using Newtonsoft.Json;
using SnakeGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnakeWorld
{
    /// <summary>
    /// a class representing the information of a powerup
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Powerup
    {
        [JsonProperty]
        public int power { get; private set; } //id
        [JsonProperty]
        public Vector2D loc { get; private set; } = new(); //the location
        [JsonProperty]
        public bool died { get; private set; } //boolean flag to determine if died

        public Powerup()
        {
            //for jason :)
        }

        /// <summary>
        /// a server side constructor
        /// </summary>
        /// <param name="power"></param>
        /// <param name="loc"></param>
        public Powerup(int power, Vector2D loc)
        {
            this.power = power;
            this.loc = loc;
            died = false;
        }

        /// <summary>
        /// sets the died flag on the powerup
        /// </summary>
        public void Die()
        {
            died = true;
        }
    }
}
