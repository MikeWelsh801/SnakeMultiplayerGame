using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using IImage = Microsoft.Maui.Graphics.IImage;
#if MACCATALYST
using Microsoft.Maui.Graphics.Platform;
#else
using Microsoft.Maui.Graphics.Win2D;
#endif
using Color = Microsoft.Maui.Graphics.Color;
using System.Reflection;
using Microsoft.Maui;
using System.Net;
using Font = Microsoft.Maui.Graphics.Font;
using SizeF = Microsoft.Maui.Graphics.SizeF;
using SnakeWorld;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Maui.Controls;
using Windows.Media.Devices;
using System.Media;
using Microsoft.UI.Dispatching;

namespace SnakeGame;

/// <summary>
/// a class library for drawing the world
/// </summary>
public class WorldPanel : IDrawable
{
    // Images
    private IImage wall;
    private IImage background;

    // Animations for fire
    private IImage[] fire;
    private float fireIndex;

    //Animations for powerups
    private IImage[] fireSpin;
    private float fireSpinIndex = 0;

    //store the world and the view size
    private World world;
    private int viewSize = 900;
    private bool initializedForDrawing = false;

    //a random used for the rave snake
    private Random r = new();

    // A delegate for DrawObjectWithTransform
    // Methods matching this delegate can draw whatever they want onto the canvas  
    public delegate void ObjectDrawer(object o, ICanvas canvas);

    public float Scale { get; set; } = 1;


#if MACCATALYST
    private IImage loadImage(string name)
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeGame.Resources.Images";
        return PlatformImage.FromStream(assembly.GetManifestResourceStream($"{path}.{name}"));
    }
#else
    private IImage loadImage( string name )
    {
        Assembly assembly = GetType().GetTypeInfo().Assembly;
        string path = "SnakeGame.Resources.Images";
        var service = new W2DImageLoadingService();
        return service.FromStream( assembly.GetManifestResourceStream( $"{path}.{name}" ) );
    }
#endif

    /// <summary>
    /// a defaul constructor
    /// </summary>
    public WorldPanel()
    {
        //stub, nothing needs to be intialized here
    }

    /// <summary>
    /// a private method that loads all of the images needed for drawing
    /// </summary>
    private void InitializeDrawing()
    {
        //load the wall image
        wall = loadImage( "WallSprite.png" );

        //load the background image
        background = loadImage( "Background.BGOffice1.png" );

        //load the fire folder
        fireIndex = 0;
        fire = new IImage[61];
        for (int i = 0; i < 61; i++)
            fire[i] = loadImage($"Fire.fire{i}.png");

        //load the firespin folder
        fireSpin = LoadAnimation("FireSpin", 61);

        initializedForDrawing = true;
    }


    /// <summary>
    /// This can be used to easily load animations in a file.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="frameCount"></param>
    /// <returns>an array of animation frames</returns>
    private IImage[] LoadAnimation(string name, int frameCount)
    {
        // Load all of the images and store them in an array
        IImage[] result = new IImage[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            if(i < 10)
                result[i] = loadImage($"{name}.tile00{i}.png");
            else if(i < 100)
                result[i] = loadImage($"{name}.tile0{i}.png");
            else
                result[i] = loadImage($"{name}.tile{i}.png");
        }
        return result;
    }

    /// <summary>
    /// The method that does most of the work drawing everything
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="dirtyRect"></param>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        //if images haven't loaded, load em 
        if ( !initializedForDrawing )
            InitializeDrawing();

        // undo any leftover transformations from last frame
        canvas.ResetState();
        canvas.FillColor = new Color(68, 68, 68);
        canvas.FillRectangle(0, 0, 2000, 2000);

        //if we haven't heard from the server, don't start drawing
        if (world == null || !world.Players.ContainsKey(world.PlayerID))
            return;

        lock (world)
        {
            //get the location of the player
            Snake player = world.Players[world.PlayerID];
            float playerX = (float)player.body[player.body.Count-1].GetX();
            float playerY = (float)player.body[player.body.Count - 1].GetY();

            // reset zoom on death
            if (player.died)
                Scale = 1;
            
            // Deal with zoom
            if (Scale > 0.35)
            {
                // center the view on the middle of the world
                canvas.Scale(Scale, Scale);
                canvas.Translate(((float)viewSize / 2) - playerX, ((float)viewSize / 2) - playerY);
            }  
            else
            {
                // if view is zoomed way out just put the map in the middle of the gridView
                canvas.Translate(((float)viewSize / 2), ((float)viewSize / 2) - 150);
                canvas.Scale(Scale, Scale);
            }


            //draw the background
            canvas.DrawImage(background, -world.Size / 2, -world.Size / 2, world.Size, world.Size);

            //draw the walls
            foreach(var wall in world.Walls.Values)
            {
                DrawObjectWithTransform(canvas, wall, wall.getTopX(), wall.getTopY(), 0, WallDrawer);
            }

            //draw the powerups
            foreach (Powerup p in world.Powerups.Values)
            {
                DrawObjectWithTransform(canvas, p, p.loc.X, p.loc.Y, 0, PowerupDrawer);
            }

            //draw the snakes
            foreach (Snake s in world.Players.Values)
            {
                if (!s.alive)
                {
                    //death animation
                    DrawObjectWithTransform(canvas, fire, s.body.Last().X, s.body.Last().Y, 0, FireDrawer);

                    //dont draw the snake
                    continue;
                }

                // Set the Stroke Color, etc, based on s's ID
                canvas.StrokeColor = snakeColor(s.snake);

                // Loop through snake segments, calculate segment length and segment direction
                for (int i = 0; i<s.body.Count-1; i++)
                {
                    //calculate segment length
                    Vector2D v1 = s.body[i];
                    Vector2D v2 = s.body[i + 1];

                    // FIX LOOPING PROBLEM!!! This is janky, and doesn't work well.
                    if (v1.X * v2.X < world.Size * world.Size / 4 * -1)
                    {
                        DrawLoopedX(canvas, v1, v2);
                        continue;
                    }
                    if(v1.Y * v2.Y < world.Size * world.Size / 4 * -1)
                    {
                        DrawLoopedY(canvas, v1, v2);
                        continue;
                    }

                    Vector2D dir = v2 - v1;
                    
                    double segmentLength = dir.Length();

                    //normalize the vector, clamp can be used because dir is always cardinally alligned
                    dir.Clamp();



                    //draw the object
                    DrawObjectWithTransform(canvas, segmentLength, v1.X, v1.Y, dir.ToAngle(), SnakeSegmentDrawer);
                }

                //draw the name and score
                DrawObjectWithTransform(canvas, s, s.body.Last().X, s.body.Last().Y, 0, nameScoreDrawer);
            }
        }
    }

    /// <summary>
    /// Handles a horizontal loop and draws the segments.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    private void DrawLoopedX(ICanvas canvas, Vector2D v1, Vector2D v2)
    {
        // Break tail segment into new segment
        Vector2D v1prime = new(v1);
        // set new vector to edge of screen
        if (v1.X > 0)
            v1prime.X = world.Size/2;
        else v1prime.X = -world.Size/2;

        // Draw tail segment
        Vector2D d1 = v1prime - v1;
        double l1 = d1.Length();
        d1.Clamp();
        DrawObjectWithTransform(canvas, l1, v1.X, v1.Y, d1.ToAngle(), SnakeSegmentDrawer);

        // Break head segment into new segment
        Vector2D v2prime = new(v2);

        // Set new vector to edge of screen
        if (v2.X > 0)
            v2prime.X = world.Size/2;
        else v2prime.X = -world.Size/2;

        // Draw head segment
        Vector2D d2 = v2 - v2prime;
        double l2 = d2.Length();
        d2.Clamp();
        DrawObjectWithTransform(canvas, l2, v2prime.X, v2prime.Y, d2.ToAngle(), SnakeSegmentDrawer);
    }


    /// <summary>
    /// Handles a vertical loop and draws the segments.
    /// </summary>
    /// <param name="canvas"></param>
    /// <param name="v1"></param>
    /// <param name="v2"></param>
    private void DrawLoopedY(ICanvas canvas, Vector2D v1, Vector2D v2)
    {
        // Break tail segment into new segment
        Vector2D v1prime = new(v1);
        // set new vector to edge of screen
        if (v1.Y > 0)
            v1prime.Y = world.Size / 2;
        else v1prime.Y = -world.Size / 2;

        // Draw tail segment
        Vector2D d1 = v1prime - v1;
        double l1 = d1.Length();
        d1.Clamp();
        DrawObjectWithTransform(canvas, l1, v1.X, v1.Y, d1.ToAngle(), SnakeSegmentDrawer);

        // Break head segment into new segment
        Vector2D v2prime = new(v2);

        // Set new vector to edge of screen
        if (v2.Y > 0)
            v2prime.Y = world.Size / 2;
        else v2prime.Y = -world.Size / 2;

        // Draw head segment
        Vector2D d2 = v2 - v2prime;
        double l2 = d2.Length();
        d2.Clamp();
        DrawObjectWithTransform(canvas, l2, v2prime.X, v2prime.Y, d2.ToAngle(), SnakeSegmentDrawer);
    }

    /// <summary>
    /// a private delegate for drawing the fire animation
    /// </summary>
    /// <param name="o"></param>
    /// <param name="canvas"></param>
    private void FireDrawer(object o, ICanvas canvas)
    {
        //draw the animation frame
        canvas.DrawImage(fire[(int)fireIndex], -100, -140, fire[(int)fireIndex].Width *2,
            fire[(int)fireIndex].Height*2);

        //update the index
        fireIndex += 0.3f;
        if (fireIndex > fire.Length)
            fireIndex = 0;
    }

    /// <summary>
    /// This method performs a translation and rotation to draw an object.
    /// </summary>
    /// <param name="canvas">The canvas object for drawing onto</param>
    /// <param name="o">The object to draw</param>
    /// <param name="worldX">The X component of the object's position in world space</param>
    /// <param name="worldY">The Y component of the object's position in world space</param>
    /// <param name="angle">The orientation of the object, measured in degrees clockwise from "up"</param>
    /// <param name="drawer">The drawer delegate. After the transformation is applied, the delegate is invoked to draw whatever it wants</param>
    private void DrawObjectWithTransform(ICanvas canvas, object o, double worldX, double worldY, double angle, ObjectDrawer drawer)
    {
        // "push" the current transform
        canvas.SaveState();

        canvas.Translate((float)worldX, (float)worldY);
        canvas.Rotate((float)angle);
        drawer(o, canvas);

        // "pop" the transform
        canvas.RestoreState();
    }

    /// <summary>
    /// a private delegate for drawing the snake segements
    /// </summary>
    /// <param name="o"></param>
    /// <param name="can"></param>
    private void SnakeSegmentDrawer(object o, ICanvas can)
    {
        //get the length
        int segmentLength = Convert.ToInt32(o);

        //set the stroke
        can.StrokeSize = 10;
        can.StrokeLineCap = LineCap.Round;

        //draw the segment
        can.DrawLine(0, 0, 0, -segmentLength);
    }

    /// <summary>
    /// a private delegate for displaying names and scores of snakes
    /// </summary>
    /// <param name="o"></param>
    /// <param name="can"></param>
    private void nameScoreDrawer(object o, ICanvas can)
    {
        //get the snake
        Snake s = o as Snake;

        //extract name and score
        string text = s.name + ": " + s.score;

        //set the canvas
        can.FontColor = Colors.White;
        can.FontSize = 15;
        can.Font = Font.DefaultBold;


        if(s.dir.Y > 0) //snake is going down
            can.DrawString(text, 0, 15, HorizontalAlignment.Center); //draw the name below the snake
        else
            can.DrawString(text, 0, -15, HorizontalAlignment.Center); //draw the name above the snake
    }

    /// <summary>
    /// a delegate for drawing the walls
    /// </summary>
    /// <param name="o"></param>
    /// <param name="can"></param>
    private void WallDrawer(object o, ICanvas can)
    {
        Wall p = o as Wall;

        // scale the ships down a bit
        float w = (float)p.getWidth();
        float h = (float)p.getHeight();

        // Images are drawn starting from the top-left corner.
        // So if we want the image centered on the player's location, we have to offset it
        // by half its size to the left (-width/2) and up (-height/2)

        if(w < 51)
        {
            //tile by height
            for (int i = 0; i <= h; i += 50)
                can.DrawImage(wall, -25, i-25, w, w);
        }
        else
        {
            //tile by width
            for (int i = 0; i <= w; i += 50)
                can.DrawImage(wall, i - 25, -25, h, h);
        }
    }

    /// <summary>
    /// a delegate for drawing powerups
    /// </summary>
    /// <param name="o"></param>
    /// <param name="can"></param>
    private void PowerupDrawer(object o, ICanvas can)
    {
        //load the powerup
        Powerup p = o as Powerup;

        //draw the powerup at the right location
        can.DrawImage(fireSpin[(int)fireSpinIndex], -23, -23, 46,
            46);

        //increase the animation index
        fireSpinIndex += 0.08f;
        if (fireSpinIndex > fireSpin.Length)
            fireSpinIndex = 0;
    }

    /// <summary>
    /// a method for setting the World for the worldpanel
    /// </summary>
    /// <param name="w"></param>
    public void setWorld (World w)
    {
        //set the world
        world = w;
    }

    /// <summary>
    /// returns the color of a snake based on its snake ID, if the ID ends with 9, the snake will be multicolor, changing color nearly every frame. 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    private Color snakeColor(int id)
    {
        id = id%10;
        //switch statement
        return id switch
        {
            0 => Colors.Aqua,
            1 => Colors.Orange,
            2 => Colors.Indigo,
            3 => Colors.DarkRed,
            4 => Colors.LimeGreen,
            5 => Colors.LightYellow,
            6 => Colors.Plum,
            7 => Colors.White,
            8 => Colors.Black,

            //rave snake :D
            _ => Color.FromRgb(r.Next(256), r.Next(256), r.Next(256)),
        };
    }

}
