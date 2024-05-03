using Newtonsoft.Json;
using SnakeGame;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace SnakeWorld
{
    /// <summary>
    /// a class storing all information for the each player
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Snake
    {
        [JsonProperty]
        public int snake { get; private set; } = 0; //int representing unique id
        [JsonProperty]
        public string name { get; private set; } = ""; //the players name 
        [JsonProperty]
        public List<Vector2D> body { get; private set; } = new(); //the entire body of the snake
        [JsonProperty]
        public Vector2D dir { get; private set; } = new(); // a vector representing the direciton of the snake
        [JsonProperty]
        public int score { get; private set; } = 0; // the snakes score
        [JsonProperty]
        public bool died { get; private set; } = false; // a flag set if the player dies,
        [JsonProperty]
        public bool alive { get; set; } = true; // a flag set for when then player is alive
        [JsonProperty]
        public bool dc { get; set; } = false; //a flag representing disconnection
        [JsonProperty]
        public bool join { get; private set; } = true; // a flag representing joining

        private int velocity = 3; //the speed of the snake
        private long timeOfDeath = 0; //a field representing millisecond time of death
        private long respwnRate = 300; //how long it takes for the snake to respawn in milliseconds
        public bool respawn = false; //a flag representing a snake respawning
        private bool growing = false; //a flag representing a snake growing
        private int growCount = 0; //a clock to see how long a snake has left to grow
        private int growAmount = 12; //how many frames the snake gets to grow for
        private int startLength = 120; //the initial length of the snake
        private bool gamemode = false; //a flag checking if the backwards gamemode is on
        private int WorldSize; //the size of the world


        public Snake()
        {
            //default for json
        }

        
        /// <summary>
        /// a server side constructor for snakes
        /// </summary>
        /// <param name="_snake"></param>
        /// <param name="_name"></param>
        /// <param name="spawn"></param>
        /// <param name="_dir"></param>
        /// <param name="_respawnRate"></param>
        /// <param name="_velocity"></param>
        /// <param name="_startLenght"></param>
        /// <param name="_snakeGrowth"></param>
        /// <param name="_gamemode"></param>
        /// <param name="worldSize"></param>
        public Snake(int _snake, string _name, Vector2D spawn, 
            Vector2D _dir, int _respawnRate, int _velocity, int _startLenght,
            int _snakeGrowth, bool _gamemode, int worldSize)
        {
            snake = _snake;
            name = _name;
            respwnRate = _respawnRate;
            velocity = _velocity;
            NewLife(spawn, _dir);
            growAmount = _snakeGrowth;
            startLength = _startLenght;
            gamemode = _gamemode;
            WorldSize = worldSize;
        }

        /// <summary>
        /// Updates the position of the snake.
        /// </summary>
        public void update()
        {
            if (died)
            {
                died = false; 
            }
            if (!alive)
            {
                if (Stopwatch.GetTimestamp() - timeOfDeath > respwnRate * 100_000)
                    respawn = true;
                else
                    return;
            }

            // Remove the tail if it catches the body
            if ((body[0] - body[1]).Length() < velocity)
                body.RemoveAt(0);

            //move the tail
            if (!growing)
            {
                Vector2D tailDir = (body[1] - body[0]);
                tailDir.Clamp();
                body[0] += tailDir * velocity;
            }
            else
            {
                //don't move the tail if the snake is growing
                growCount++;
                if (growCount == growAmount)
                    growing = false;
            }
            //move the head
            body[^1] += dir * velocity;
            CheckOnEdge();
        }

        /// <summary>
        /// this method checks if a snake is on the edge of the map, and changes the body vectors accordingly
        /// </summary>
        private void CheckOnEdge()
        {
            Vector2D head = body[^1];
            // going left
            if (body[^1].X < -WorldSize/2)
            {
                //body.RemoveAt(body.Count - 1);
                //start on the right side
                body.Add(new(WorldSize/2, head.Y));
                body.Add(new(WorldSize/2 - velocity, head.Y));
            }
            // going right
            else if(body[^1].X > WorldSize/2)
            {
                body.Add(new(-WorldSize / 2, head.Y));
                body.Add(new(-WorldSize / 2 + velocity, head.Y));
            }
            // going up
            else if (body[^1].Y < -WorldSize / 2)
            {
                body.Add(new(head.X, WorldSize / 2));
                body.Add(new(head.X, WorldSize/2 - velocity));
            }
            // going down
            else if (body[^1].Y > WorldSize / 2)
            {
                body.Add(new(head.X, -WorldSize / 2));
                body.Add(new(head.X, -WorldSize / 2 + velocity));
            }

            //check the tail if its gone past the border, get rid of it
            if (body[0].X < -WorldSize / 2 || body[0].X > WorldSize / 2 ||
                body[0].Y < -WorldSize / 2 || body[0].Y > WorldSize / 2)
            {
                body.RemoveAt(0);
                if(body.Count > 2)
                    body.RemoveAt(0);
            }
        }

        

        /// <summary>
        /// Check collisions with powerups
        /// </summary>
        /// <param name="pow"></param>
        /// <returns></returns>
        public bool CheckPowerupCollision(Powerup pow)
        {
            if (body[^1].X < pow.loc.X + 13 && body[^1].X > pow.loc.X - 13 &&
                body[^1].Y < pow.loc.Y + 13 && body[^1].Y > pow.loc.Y - 13)
            {
                growing = true;
                growCount = 0;
                score++;
                if (gamemode)
                    flipSnake();
                return true;
            }
            return false;
        }

        /// <summary>
        /// when gamemode is true, this method will turn the snake around 
        /// </summary>
        private void flipSnake()
        {
            //find the new dir
            dir = body[0] - body[1];
            
            // if the tail has caught up with the last node
            // causing the direction vector to be too small remove 
            // the last node before normalizing
            while (dir.Length() < 1)
            {
                body.RemoveAt(0);
                dir = body[0] - body[1];
            }
            dir.Normalize();


            //flip body
            body.Reverse();

        }

        /// <summary>
        /// Check collisions with walls
        /// </summary>
        /// <param name="values"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void CheckWallCollisions(Dictionary<int, Wall>.ValueCollection values)
        {
            //for each wall, check if the head has collided
            foreach (Wall w in values)
                if (w.IsCollided(body[^1]))
                {
                    KillSelf();
                }
        }

        /// <summary>
        /// Check collisions with snakes
        /// </summary>
        /// <param name="values"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void CheckSnakeCollisions(Dictionary<int, Snake>.ValueCollection snakes)
        {
            foreach(Snake s in snakes)
            {
                // check yo self
                if (s.snake == snake)
                    CheckSelfCollision();
                // only check living snakes
                else if(s.alive)
                {
                    for (int i = 0; i < s.body.Count - 1; i++)
                    {
                        checkSegmentCollision(s.body[i], s.body[i + 1]);
                    }
                }
            }
        }
        /// <summary>
        /// Sets snake to dead and starts the respawn timer.
        /// </summary>
        private void KillSelf()
        {
            died = true;
            alive = false;
            timeOfDeath = Stopwatch.GetTimestamp();
            score = 0;
        }

        /// <summary>
        /// Checks a snake's collision with itself
        /// </summary>
        public void CheckSelfCollision()
        {
            //don't check for collision on a single line snake
            if (body.Count == 2)
                return;

            bool canCollide = false;
            //iterate through body backwards
            for(int i = body.Count - 2; i > 0; i--)
            {
                //check if the vector is opposite dir
                if (!canCollide)
                {
                    //check if the vector is opposite of dir
                    Vector2D tempDir = body[i] - body[i - 1];
                    tempDir.Clamp();
                    if (tempDir.Dot(dir) == -1)
                        //if it is, set canColldie to true,
                        canCollide = true;

                }
                else
                {
                    //do some more collision junk :(
                    checkSegmentCollision(body[i], body[i - 1]);
                }
            }
        }


        /// <summary>
        /// chekcs if a segment  made of 2 vectors has collided with the head of this snake
        /// </summary>
        /// <param name="v1"></param>
        /// <param name="v2"></param>
        private void checkSegmentCollision(Vector2D v1, Vector2D v2)
        {
            //setup the vector
            Vector2D temp = v1 - v2;
            temp.Clamp();
            //if its dot  product is zero, its collidable
            if(temp.Dot(dir) == 0)
            {
                //setup the rectanlge
                double minX;
                double minY;
                double maxX;
                double maxY;
                if (v1.X > v2.X || v2.Y > v1.Y)
                {
                    //the vector is going left or up
                    minX = v2.X - 10;
                    maxY = v2.Y + 10;
                    maxX = v1.X + 10;
                    minY = v1.Y - 10;
                }
                else
                {
                    //the vector is going right or down
                    minX = v1.X - 10;
                    maxY = v1.Y + 10;
                    maxX = v2.X + 10;
                    minY = v2.Y - 10;
                }
                if (body[^1].X > minX && body[^1].X < maxX
                    && body[^1].Y > minY && body[^1].Y < maxY)
                {
                    KillSelf();
                }
            }
            return;
        }

        /// <summary>
        /// changes the direction of the snake based on the message
        /// </summary>
        /// <param name="message"></param>
        public void ChangeDir(string message)
        {
            if (!alive || (body[^1] - body[^2]).Length() < 11)
                return;

            if(message.Contains("right") && dir.X == 0)
            {
                //change direction
                dir = new(1, 0);
                //add new vector
                body.Add(body[^1]);
            }
            else if(message.Contains("left") && dir.X == 0)
            {
                dir = new(-1, 0);
                //add new vector
                body.Add(body[^1]);
            }
            else if(message.Contains("down") && dir.Y == 0)
            {
                dir = new(0, 1);
                //add new vector
                body.Add(body[^1]);
            }
            else if(message.Contains("up") && dir.Y == 0)
            {
                dir = new(0, -1);
                //add new vector
                body.Add(body[^1]);
            }
        }


        /// <summary>
        /// spawns a snake in a new place
        /// </summary>
        /// <param name="spawn"></param>
        /// <param name="_dir"></param>
        public void NewLife(Vector2D spawn, Vector2D _dir)
        {
            //no need to respawn anymore
            respawn = false;
            alive = true;
            body.Clear();
            dir = _dir;

            //setup vector list
            body.Add(spawn);
            spawn += (dir * startLength);
            body.Add(spawn);
        }
    }
}
