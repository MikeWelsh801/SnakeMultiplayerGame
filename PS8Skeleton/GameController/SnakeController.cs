using NetworkUtil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using SnakeWorld;

namespace GameController
{
    /// <summary>
    /// a class that handles talking to the view and model of the snake game
    /// </summary>
    public class SnakeController
    {
        //these get set initially to -1 to simplify handshake
        public int playerID = -1;
        public int worldSize = -1;
        private string playerName = "";

        //reference to the world
        public World world { get; private set; } = new(0, 0); //it gets set to prevent null warnings

        // Controller events that the view can subscribe to
        public delegate void MessageHandler();
        public event MessageHandler? MessagesArrived;

        public delegate void ConnectedHandler(World world);
        public event ConnectedHandler? Connected;

        public delegate void ErrorHandler(string err);
        public event ErrorHandler? Error;

        /// <summary>
        /// State representing the connection with the server
        /// </summary>
        SocketState? theServer = null;

        /// <summary>
        /// Begins the process of connecting to the server
        /// </summary>
        /// <param name="addr"></param>
        public void Connect(string addr, string name)
        {
            //set player name
            playerName = name!;

            //connect to server,once server is connected, send the server the player name
            Networking.ConnectToServer(OnConnect, addr, 11000);

            //wait for a world size, and then start building
        }

        /// <summary>
        /// a method to inform the server of the input from the view
        /// </summary>
        /// <param name="dir"></param>
        public void Move(string dir)
        {
            string send = "";

            if (dir == "w")
            {
                send = "{\"moving\":\"up\"}\n";
            }
            else if (dir == "a")
            {
                send = "{\"moving\":\"left\"}\n";
            }
            else if (dir == "s")
            {
                send = "{\"moving\":\"down\"}\n";
            }
            else if (dir == "d")
            {
                send = "{\"moving\":\"right\"}\n";
            }

            //if w, a, s, or d was not sent, don't tell the server about it
            else return;
            
            //send the correct json to the server
            Networking.Send(theServer!.TheSocket, send);
        }


        /// <summary>
        /// Method to be invoked by the networking library when a connection is made
        /// </summary>
        /// <param name="state"></param>
        private void OnConnect(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                // inform the view
                Error?.Invoke("Error connecting to server");
                return;
            }
            //send the name to server 
            Networking.Send(state.TheSocket, playerName);

            theServer = state;

            // Start an event loop to receive messages from the server
            state.OnNetworkAction = ReceiveMessage;
            Networking.GetData(state);
        }

        /// <summary>
        /// Method to be invoked by the networking library when 
        /// data is available
        /// </summary>
        /// <param name="state"></param>
        private void ReceiveMessage(SocketState state)
        {
            if (state.ErrorOccurred)
            {
                // inform the view
                Error?.Invoke("Lost connection to server");
                return;
            }
            ProcessMessages(state);

            // Continue the event loop
            // state.OnNetworkAction has not been changed, 
            // so this same method (ReceiveMessage) 
            // will be invoked when more data arrives
            Networking.GetData(state);
        }

        /// <summary>
        /// Process any buffered messages separated by '\n'
        /// Then inform the view
        /// </summary>
        /// <param name="state"></param>
        private void ProcessMessages(SocketState state)
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


                //this is either player ID or world size
                if (playerID < 0)
                    playerID = int.Parse(p);
                //if player ID has been set, and a json object wasn't sent it must be the world size
                else if (worldSize < 0)
                {
                    worldSize = int.Parse(p);
                    //intialize the world
                    world = new World(worldSize, playerID);
                    // inform the view
                    Connected?.Invoke(world);
                }

                //do some deserialization :3
                else
                {
                    JObject obj = JObject.Parse(p);
                    if (obj["snake"] is not null)
                    {
                        // build snake
                        Snake s = JsonConvert.DeserializeObject<Snake>(p)!;

                        //update
                        world.Update(s);

                    }
                    if (obj["wall"] is not null)
                    {
                        // build wall
                        Wall w = JsonConvert.DeserializeObject<Wall>(p)!;

                        //update
                        world.Update(w);

                    }
                    if (obj["power"] is not null)
                    {
                        // build power
                        Powerup pow = JsonConvert.DeserializeObject<Powerup>(p)!;

                        //update
                        world.Update(pow);
                    }
                }
                

                // Then remove it from the SocketState's growable buffer
                state.RemoveData(0, p.Length);
            }

            // inform the view
            MessagesArrived?.Invoke();

        }

        /// <summary>
        /// Closes the connection with the server
        /// </summary>
        public void Close()
        {
            theServer?.TheSocket.Close();
        }

        /// <summary>
        /// Send a message to the server
        /// </summary>
        /// <param name="message"></param>
        public void MessageEntered(string message)
        {
            if (theServer is not null)
                Networking.Send(theServer.TheSocket, message + "\n");
        }

    }
}