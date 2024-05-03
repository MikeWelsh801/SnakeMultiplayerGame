using SnakeGame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Runtime.Serialization;

namespace SnakeWorld
{

    /// <summary>
    /// a class storing all information for a wall
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    [DataContract(Namespace ="")]
    public class Wall
    {
        [JsonProperty(PropertyName ="wall")]
        [DataMember]
        public int ID { get; private set; } = 0; //id
        [JsonProperty]
        [DataMember]
        public Vector2D p1 { get; private set; } = new();//endpoint of wall
        [JsonProperty]
        [DataMember]
        public Vector2D p2 { get; private set; } = new(); //other endpoint of wall

        public Wall()
        {
            //default for jason :)
        }

        /// <summary>
        /// returns the X value of the top left corner of the wall ofr drawing
        /// </summary>
        /// <returns></returns>
        public double getTopX()
        {
            return Math.Min(p1.GetX(), p2.GetX());
        }
        

        /// <summary>
        /// returns the Y value of the top left corner of the wall for drawing
        /// </summary>
        /// <returns></returns>
        public double getTopY()
        {
            return Math.Min(p1.GetY(), p2.GetY());
        }

        /// <summary>
        /// returns the width of the wall
        /// </summary>
        /// <returns></returns>
        public double getWidth()
        {
            if (p1.X == p2.X)
                return 50;
            else return Math.Abs(p1.X - p2.X);
        }

        /// <summary>
        /// returns the height of the wall
        /// </summary>
        /// <returns></returns>
        public double getHeight()
        {
            if (p1.Y == p2.Y)
                return 50;
            else return Math.Abs(p1.Y - p2.Y);
        }

        /// <summary>
        /// determines if a vector has collided with the wall object
        /// </summary>
        /// <param name="head"></param>
        /// <returns></returns>
        public bool IsCollided(Vector2D head)
        {
            //construct range
            double minX = getTopX() - 30;
            double minY = getTopY() - 30;
            double maxX = getTopX() + Math.Abs(p2.X - p1.X) + 30;
            double maxY = getTopY() + Math.Abs(p2.Y-p1.Y) +30;

            //if it collides return true
            if (head.X < maxX && head.X > minX && head.Y > minY && head.Y < maxY)
                return true;

            //if it doesnt collide return false
            return false;
        }
    }
}
