# game-hebrew_nationals_game

* a team (2 person) game project I worked on for CS3500

## Game Play

![Snake Game Play](./screenshots/GamePlay.gif?raw=true "Snake Game Play")

## Description

game-hebrew_nationals_game created by GitHub Classroom

This is a networked multiplayer snake game, that allows the user to connect to a server, and play snake with others and collect powerups which make the snake
longer. 

[Client ps8]
The client is able to send WASD inputs to the server as move commands. We moved all logic for this into the conroller. The view just passes this along. We don't have support for 
any other inputs (i.e. zoom) everything else is ignored. 

We parse the handshake in the controller before creating the model (world class). Our model has three classes in addition to the world, (snake, wall, powerup). Our wall class
has 4 methods, GetTopX, GetTopY, GetHeight, GetWidth. These help in the drawing method in the view. 

The view has a reference to the world, which isn't strict MVC, but we felt it necessary to facilitate easy drawing. This is similar to what the instructors did in class.

We fixed looping so that the snake draws correctly, but if you keep looping in a corner, the snake's hit detection fails and sometimes the snake will be drawn accross the entire screen
we witnessed congruent bugs with the provided client (snake client), so we do not see it as a failure in our code. 

[design choices]
We choose snake colors based on player ID modded by 10 so every 10th player will be the same color. We also added a RAVE!!!!! snake that randomly chooses a color every server update.

We got the background using AI, we used craiyon.com (formerly dalle mini) with the prompt "top down cyberpunk office map".

We decided to use animated powerups. We attempted an animated shadow effect under player name/score, but it caused a lot of lag, so it was removed. The colors for maui were changed to look darker. Instead of an explosion
for a snake death, we went with an animated fire effect. 

Our particle effects were from @DavitMasia and @CodeManuPro on https://codemanu.itch.io/pixelart-effect-pack.

All code is from class other than code we wrote.


[Client PS9]

To start the server, we parse a settings xml file, which contains information on the walls, respawning, powerup counts, framecount, etc

our server will print the frames per second every second

We establish a handshake to handle client connections and disconnections

Our settings file includes a gamemode parameter, which when set to true enables our backwards snake game

The backwards snake game was inspired by googles snake, when the snake picks up a powerup, it will start moving froms its tail, flipping the entire snake. 

We choose spawn locations for snakes and powerups by creating a rectangle and checking it's collisions with walls. Then we create a series of snakes and check if they collided with other snakes. This keeps the powerups and snakes from spawning too close to walls, and other snakes. We've tested to make sure that powerups do not spawn in walls. 

We use the same world, snake, walls and powerups that we used for the client. We added an updateWorld() method that handles the server side updates. We added a plethora of methods for handling collisions, movement, respawns, score to the snake class. This is mainly implemented through the world calling the snake's update method. 

Collisions work. Looping from one side of the other works. 

Our server does appear to start to slow at around 15 players.
