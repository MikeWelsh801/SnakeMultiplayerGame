using NetworkUtil;
using System.IO.Pipes;
using SnakeWorld;
using SnakeGame;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using Newtonsoft.Json;
using System.Xml;
using System.Diagnostics;

namespace Server
{
    /// <summary>
    /// This class is the snake server, its used to setup handshakes from the server to the client, and send and receive gamedata for clients
    /// </summary>
    internal class SnakeServer
    {
        //a list of clients
        private Dictionary<long, SocketState> clients;

        //a world storing all the game data
        private World world;

        //a list used to remove diconnected clients at an appropriate time
        private HashSet<long> disconnectedClients = new HashSet<long>();

        static void Main(string[] args)
        {
            
            //set the path to .net directory
            string path = Path.GetFullPath("settings.xml");
            SnakeServer s;
            if (File.Exists(path))
            {
                //create a new server and start it
                s = new(path);
            }
            else
            {
                // if it's not with the executable check the directory
                // with .cs file
                path = Path.GetFullPath("../../../settings.xml");
                //create a new server and start it
                s = new(path);
            }

            s.StartServer();
            //keep main running
            s.update();
        }

        /// <summary>
        /// this method begins the event loop to listen for new clients
        /// </summary>
        private void StartServer()
        {
            // This begins an "event loop"
            Networking.StartServer(handleNewClient, 11000);

            //Inform the user that the server is running
            Console.WriteLine("server has started :3");

        }

        /// <summary>
        /// this class represents a snakeserver object, which all the methods outside of main will be called on
        /// this constructor is used to allow a static main method, and non static private methods. 
        /// </summary>
        /// <param name="filepath"></param>
        public SnakeServer(string filepath)
        {
            //initalize dictionary
            clients = new();

            //intialize world
            world = new(filepath);
        }

        /// <summary>
        /// this method waits until an appropriate amount of time has passed, and then updates the world object
        /// before broadcasting the updated world object to all clients, this method also handles checking for disconnections
        /// </summary>
        private void update()
        {
            // create stopwatches for update loop and fps
            Stopwatch watch = new();
            Stopwatch tick = new();
            watch.Start();
            tick.Start();
            
            //do an update loop
            while(true)
            {
                //let a timer spin for the ammount of time the settings determines
                while (watch.ElapsedMilliseconds < world.settings.MSPerFrame)
                {
                    //stub
                }
                // print the fps every second
                if(tick.ElapsedMilliseconds > 1000)
                {
                    int fps = (int)(1000 / watch.ElapsedMilliseconds);
                    
                    Console.WriteLine("FPS: " + fps);
                    tick.Restart();
                }
                
                //set the watch back to zero
                watch.Restart();
                
                //update the world
                world.UpdateWorld();

                //remove stragglers
                RemoveDisconnectedClients();

                //send everyone the world
                lock (world)
                {
                    foreach (SocketState s in clients.Values)
                    {
                        //if the player has disconnected, add to the list to be removed and don't send to it
                        if (world.Players[(int)s.ID].dc)
                        {
                            disconnectedClients.Add(s.ID);
                            continue;
                        }
                        foreach (Snake snek in world.Players.Values)
                        {
                            if(!Networking.Send(s.TheSocket, JsonConvert.SerializeObject(snek) + "\n"))
                            {
                                //inform the clients the snake has disconnected
                                world.Players[(int)s.ID].dc = true;
                                //tells the client to stop drawing snake
                                world.Players[(int)s.ID].alive = false; 
                            }
                        }

                        foreach (Powerup p in world.Powerups.Values)
                            Networking.Send(s.TheSocket, JsonConvert.SerializeObject(p) + "\n");
                    }
                }

                lock (world)
                {
                    //get rid of dead powerups after its all been sent
                    world.removeDeadPows();
                }

            }
        }

        /// <summary>
        /// this method handles a new client connection, and sets the network actions for the socket state apporiately
        /// </summary>
        /// <param name="state"></param>
        private void handleNewClient(SocketState state)
        {
            if (state.ErrorOccurred)
                return;
            lock (world)
            {
                //check if the world has the iD
                if (world.Players.ContainsKey((int)state.ID))
                {
                    //stub
                }
                else
                {
                    //get the playername
                    state.OnNetworkAction = receivePlayerName;
                }
            }
            Networking.GetData(state);
        }

        /// <summary>
        /// this method is a delegate for the socket states network action, it allows the world to store a reference to the players name
        /// </summary>
        /// <param name="state"></param>
        private void receivePlayerName(SocketState state)
        {
            // Remove the client if they aren't still connected
            if (state.ErrorOccurred)
            {
                //the snake has not joined the game yet, no need to do anything
                return;
            }

            //process the player name
            ProcessName(state);
        }

        /// <summary>
        /// this method is used to handle connected clients, once the name and player ID have been set
        /// </summary>
        /// <param name="state"></param>
        private void handleDataFromClient(SocketState state)
        {
            // Remove the client if they aren't still connected
            if (state.ErrorOccurred)
            {
                return;
            }

            //process the player name
            ProcessData(state);

            // Continue the event loop that receives messages from this client
            Networking.GetData(state);
        }

        /// <summary>
        /// Given the data that has arrived so far, 
        /// potentially from multiple receive operations, 
        /// determine if we have enough to make a complete message,
        /// and process it (print it and broadcast it to other clients).
        /// </summary>
        /// <param name="sender">The SocketState that represents the client</param>
        private void ProcessName(SocketState state)
        {
            string totalData = state.GetData();

            string[] parts = Regex.Split(totalData, @"(?<=[\n])");

            // Loop until we have processed all messages.
            // We may have received more than one.
            foreach (string p in parts)
            {
                // Ignore empty strings added by the regex splitter
                if (p.Length == 0)
                    continue;
                //// The regex splitter will include the last string even if it doesn't end with a '\n',
                //// So we need to ignore it if this happens. 
                //if (p[p.Length - 1] != '\n')
                //    break;

                // Remove it from the SocketState's growable buffer
                state.RemoveData(0, p.Length);


                // get rid of enter and add snake with correct name
                string name = p.Trim('\n');
                lock (world) 
                {
                    var spawn = world.pickSpawn();
                    world.Players.Add((int)state.ID, new Snake((int)state.ID, name, 
                        spawn.Item1, spawn.Item2, world.settings.RespawnRate, 
                        world.settings.Velocity, world.settings.StartingLength, 
                        world.settings.SnakeGrowth, world.settings.Gamemode,
                        world.Size));
                }

                // let user know they've connected.
                Console.WriteLine($"{name} connected to the server as player: {state.ID}.");

                //send ID
                Networking.Send(state.TheSocket, state.ID.ToString() + "\n");

                //send worldsize
                Networking.Send(state.TheSocket, world.Size.ToString() + "\n");

                //send walls
                lock (world)
                {
                    foreach (Wall w in world.Walls.Values)
                        Networking.Send(state.TheSocket, JsonConvert.SerializeObject(w) + "\n");
                }

                //walls have now been sent, client can now be updated
                // Save the client state
                // Need to lock here because clients can disconnect at any time
                lock (world)
                {
                    clients[state.ID] = state;
                }
                //change action
                state.OnNetworkAction = handleDataFromClient;

                //keep asking 
                Networking.GetData(state);
            }
        }

        /// <summary>
        /// this method handles all inputs from connected clients
        /// </summary>
        /// <param name="state"></param>
        private void ProcessData(SocketState state)
        {
            string totalData = state.GetData();

            string[] parts = Regex.Split(totalData, @"(?<=[\n])");

            // Loop until we have processed all messages.
            // We may have received more than one.
            foreach (string p in parts)
            {
                // Ignore empty strings added by the regex splitter
                if (p.Length == 0)
                    continue;
                // The regex splitter will include the last string even if it doesn't end with a '\n',
                // So we need to ignore it if this happens. 
                if (p[p.Length - 1] != '\n')
                    break;

                // Remove it from the SocketState's growable buffer
                state.RemoveData(0, p.Length);
                
                lock (world)
                {
                    //only data receviable from client is direction 
                    //send it to the corresponding snake
                    if (world.Players.ContainsKey((int)state.ID)) 
                        world.Players[(int)state.ID].ChangeDir(p);
                }
                
            }

        }
        /// <summary>
        /// this method gets rid of disconnected clients
        /// </summary>
        private void RemoveDisconnectedClients()
        {
            //remove all disconnected clients
            foreach (long id in disconnectedClients)
                RemoveClient(id);
            disconnectedClients.Clear();
        }

        /// <summary>
        /// Removes a client from the clients dictionary
        /// </summary>
        /// <param name="id">The ID of the client</param>
        private void RemoveClient(long id)
        {
            //inform the server
            Console.WriteLine("Client " + id + " disconnected");
            lock (world)
            {
                //remove the client
                clients.Remove(id);
                world.Players.Remove((int)id);
            }
        }
    }
}