using Newtonsoft.Json.Serialization;
using SnakeGame;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;
using System.Xml.Serialization;

namespace SnakeWorld
{

    /// <summary>
    /// a class that stores all the necessary world informationfor the client
    /// </summary>
    public class World
    {
        //the walls, players, and powerups
        public Dictionary<int, Snake> Players { get;  private set; }
        public Dictionary<int, Powerup> Powerups { get; private set; }
        public Dictionary<int, Wall> Walls { get; private set; }

        //the settings
        public GameSettings settings { get; private set; } = new();

        //worldsize
        public int Size { get; private set; }
        //id of the clients snake
        public int PlayerID { get; private set; }

        //number of powerups
        public int pwrAmount = 25;
        //spawning delay for new powerups
        private int powerUpDelay = 200;
        //a counter to make sure new powerups have unique IDS
        private int currPowID = 0;
        //a representation of the newly set powerup delay
        private int currPowDelay;
        //a tick to keep track of frames since a powerup has been respawned
        private int powClock = 0;
        //a gamemode flag
        private bool backwardsMode = false;
        //a list to remove powerups in a safe manner
        List<Powerup> powToRemove = new();
        /// <summary>
        /// this constructor takes the world size and the ID of the clients snake CLIENT SIDE
        /// </summary>
        /// <param name="_size"></param>
        /// <param name="playerID"></param>
        public World(int _size, int playerID)
        {
            Players = new();
            Powerups = new();
            Walls = new();
            Size = _size;
            PlayerID = playerID;
        }

        /// <summary>
        /// a constructor that builds the world from a settings file SERVER SIDE
        /// </summary>
        /// <param name="filepath"></param>
        public World(string filepath)
        {
            lock (this)
            {
                //parse xml
                DataContractSerializer ser = new(typeof(GameSettings));
                XmlReader reader = XmlReader.Create(filepath);
                GameSettings s = (GameSettings)ser.ReadObject(reader)!; //this is not null, I initalized it 2 lines ago
                Players = new();
                Powerups = new();
                Walls = new();
                Size = s.UniverseSize;
                pwrAmount = s.MaxPowerup;
                powerUpDelay = s.MaxPowerupDelay;
                backwardsMode = s.Gamemode;

                //add the walls
                foreach (Wall w in s.Walls)
                    Walls.Add(w.ID, w);

                //add the powerups 
                while (Powerups.Count < pwrAmount)
                {
                    Powerups.Add(currPowID, new(currPowID, pickSpawn().Item1));
                    currPowID++;
                }

                //hold onto the settings
                settings = s;
            }
        }


        /// <summary>
        /// takes a snake and updates the dictionary of snakes
        /// </summary>
        /// <param name="s"></param>
        public void Update(Snake s)
        {
            lock (this)
            {
                //if the snake is already in the dictionary, update it
                if (Players.ContainsKey(s.snake))
                    Players[s.snake] = s;
                else
                    //if its not in the dictionary add it
                    Players.Add(s.snake, s);

                //if they disconnect... get em outta here 
                if (s.dc)
                    Players.Remove(s.snake);
            }
        }

        /// <summary>
        /// takes a wall and updates the dictionary of walls
        /// </summary>
        /// <param name="w"></param>
        public void Update(Wall w)
        {
            lock (this)
            {
                //if the wall is already in the dictionary, update it
                if (Walls.ContainsKey(w.ID))
                    Walls[w.ID] = w;
                //if the wall is not in the dictionary, add it
                else
                    Walls.Add(w.ID, w);
            }
        }

        /// <summary>
        /// takes a powerup and updates the dictionary of powerups
        /// </summary>
        /// <param name="p"></param>
        public void Update(Powerup p)
        {
            lock (this)
            {
                //if the dictionary contains the powerup, update it
                if (Powerups.ContainsKey(p.power))
                    Powerups[p.power] = p;
                //if the powerup is new, add it to the dictionary
                else
                    Powerups.Add(p.power, p);

                //if a snake ate the powerup, remove it
                if (p.died)
                    Powerups.Remove(p.power);
            }
        }
        /// <summary>
        /// Updates the world.
        /// </summary>
        public void UpdateWorld()
        {
            lock (this)
            {
                foreach (Snake s in Players.Values)
                {
                    //if the snake needs to be respawned
                    if (s.respawn)
                    {
                        //respawn snake
                        var spawn = pickSpawn();
                        s.NewLife(spawn.Item1, spawn.Item2);
                    }
                    // move the snake
                    s.update();

                    if (!s.alive)
                        continue;

                    // Check for collisions with walls and other snakes.
                    s.CheckSnakeCollisions(Players.Values);
                    s.CheckWallCollisions(Walls.Values);

                    //increase the respawn clock for powerup
                    powClock++;

                    // Check collisions wit powerups and kill the powerup
                    foreach(Powerup pow in Powerups.Values)
                    {
                        //if its dead get ready to remove it
                        if (pow.died)
                            powToRemove.Add(pow);
                        else if (s.CheckPowerupCollision(pow))
                        {
                            pow.Die();
                            Random r = new();
                            currPowDelay = r.Next(powerUpDelay);
                        }
                    }
                    
                    
                    //replace any removed powerups 
                    if (Powerups.Count < pwrAmount && currPowDelay <= powClock)
                    {
                        Powerups.Add(currPowID, new(currPowID, pickSpawn().Item1));
                        currPowID++;
                        powClock = 0;
                    }
                }

            }
        }

        /// <summary>
        /// removes powerups from the powToRemove list from the worlds list of powerups 
        /// </summary>
        public void removeDeadPows()
        {
            foreach (Powerup pow in powToRemove)
                Powerups.Remove(pow.power);
            powToRemove.Clear();
        }

        /// <summary>
        /// this method will determin a valid spawning location inside the world
        /// </summary>
        /// <returns></returns>
        public Tuple<Vector2D, Vector2D> pickSpawn()
        {
            //do some cool smart junk :)
            // Pick two random numbers
            Random rand = new();
            int x = rand.Next((-Size / 2) + 150, (Size / 2) - 150);
            int y = rand.Next((-Size / 2) + 150, (Size / 2) - 150);

            //pick random direction
            Vector2D dir = rand.Next(4) switch
            {
                0 => new(0, 1),
                1 => new(1, 0),
                2 => new(-1, 0),
                _ => new(0, -1),
            };

            // build temp snake
            Rectangle snake = new(x, y, 100 + (int)dir.X*100, 100 + (int)dir.Y*100);
            foreach(Wall w in Walls.Values)
            {
                // get rectangle for each wall and check if snake intersects
                Rectangle wall = new((int)w.getTopX()-125, (int)w.getTopY()-125, 
                    (int)w.getWidth() + 200, (int)w.getHeight() + 200);
                if (snake.IntersectsWith(wall))
                    return pickSpawn();
            }

            //build temp snakes
            Snake temp = new(-1, "snake", new(x, y),dir, 0, 5, 120, 2,false, Size);
            Snake temp2 = new(-1, "snake", new(x - 25, y - 25), dir, 0,5, 120, 2, false, Size);
            Snake temp3 = new(-1, "snake", new(x + 25, y - 25), dir,0, 5, 120, 2, false, Size);
            Snake temp4 = new(-1, "snake", new(x - 25, y + 25),dir, 0, 5, 120, 2, false, Size);
            Snake temp5 = new(-1, "snake", new(x - 25, y - 25),dir, 0, 5, 120, 2, false, Size);

            // check snake collisions
            temp.CheckSnakeCollisions(Players.Values);
            temp2.CheckSnakeCollisions(Players.Values);
            temp3.CheckSnakeCollisions(Players.Values);
            temp4.CheckSnakeCollisions(Players.Values);
            temp5.CheckSnakeCollisions(Players.Values);

            // collision is not detected return pickSpawn
            if (temp.alive && temp2.alive && temp3.alive && temp4.alive
                && temp5.alive)
                return new(new(x - 50, y + 50), dir);

            //if it doesn't work, do recursion
            return pickSpawn();
        }

        /// <summary>
        /// An object that stores all the fields of the settings.xml file
        /// </summary>
        [DataContract(Namespace = "", Name ="GameSettings")]
        public class GameSettings 
        {
            [DataMember]
            public int FramesPerShot { get; private set; } = 0;
            [DataMember]
            public int MSPerFrame { get; private set; } = 0;
            [DataMember]
            public int RespawnRate { get; private set; } = 0;
            [DataMember]
            public int UniverseSize { get; private set; } = 0;
            [DataMember]
            public wallList Walls { get; private set; } = new();
            [DataMember]
            public int Velocity { get; set; } = 3;
            [DataMember]
            public int MaxPowerup { get; set; } = 20;
            [DataMember]
            public int MaxPowerupDelay { get; set; } = 200;
            [DataMember]
            public int StartingLength { get; set; } = 120;
            [DataMember]
            public int SnakeGrowth { get; set; } = 12;
            [DataMember]
            public bool Gamemode { get; set; } = false;

            /// <summary>
            /// a default constructor for XML
            /// </summary>
            public GameSettings()
            {

            }
        }
        /// <summary>
        /// this class is used to parse xml into a list
        /// </summary>
        [CollectionDataContract(ItemName = "Wall", Namespace = "")]
        public class wallList : List<Wall> { }

    }
}