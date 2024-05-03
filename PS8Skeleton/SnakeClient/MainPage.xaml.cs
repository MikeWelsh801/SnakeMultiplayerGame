namespace SnakeGame;
using GameController;
using SnakeWorld;

public partial class MainPage : ContentPage
{
    //a reference to the controller
    public SnakeController sc { get; private set; }

    public MainPage()
    {
        //start drawing
        InitializeComponent();
        graphicsView.Invalidate();

        //intialize the snake controller
        sc = new SnakeController();

        //send handlers to controllers events
        sc.Error += NetworkErrorHandler;
        sc.MessagesArrived += MessagesArrivedHandler;
        sc.Connected += ConnectedHandler;
    }

    void OnTapped(object sender, EventArgs args)
    {
        keyboardHack.Focus();
    }

    /// <summary>
    /// When a keyboard input is registered, it is shipped off to the controller and deleted.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    void OnTextChanged(object sender, TextChangedEventArgs args)
    {
        //handle text send in by user
        Entry entry = (Entry)sender;
        String text = entry.Text.ToLower();

        if(text == "n")
        {
            worldPanel.Scale += 0.05f;
            

        }
        if(text == "m")
        {
            worldPanel.Scale -= 0.05f;
            
        }

        //inform the network
        sc.Move(text);

        //reset the text
        entry.Text = "";
    }

    /// <summary>
    /// a private delegate for network errors
    /// </summary>
    /// <param name="error"></param>
    private void NetworkErrorHandler(string error)
    {
        //display an alert about the network error
        Dispatcher.Dispatch(() => DisplayAlert("Error", error, "OK"));
    }

    /// <summary>
    /// a private delegate for receiving json
    /// </summary>
    private void MessagesArrivedHandler()
    {
        //draw updated objets
        Dispatcher.Dispatch(() => graphicsView.Invalidate());
    }

    /// <summary>
    /// a private delegate for connecting to the server
    /// </summary>
    /// <param name="w"></param>
    private void ConnectedHandler(World w)
    {
        //inform the world panel of the world
        worldPanel.setWorld(w);

        //disable the connection button
        Dispatcher.Dispatch(() => connectButton.IsEnabled = false);
        
    }


    /// <summary>
    /// Event handler for the connect button
    /// We will put the connection attempt logic here in the view, instead of the controller,
    /// because it is closely tied with disabling/enabling buttons, and showing dialogs.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private void ConnectClick(object sender, EventArgs args)
    {
        if (serverText.Text == "")
        {
            DisplayAlert("Error", "Please enter a server address", "OK");
            return;
        }
        if (nameText.Text == "")
        {
            DisplayAlert("Error", "Please enter a name", "OK");
            return;
        }
        if (nameText.Text.Length > 16)
        {
            DisplayAlert("Error", "Name must be less than 16 characters", "OK");
            return;
        }
        else
        {
            //if no errors in the entered text, attempt to connect to server
            sc.Connect(serverText.Text, nameText.Text);
        }
        keyboardHack.Focus();
    }


    private void ControlsButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("Controls",
                     "W:\t\t Move up\n" +
                     "A:\t\t Move left\n" +
                     "S:\t\t Move down\n" +
                     "D:\t\t Move right\n" +
                     "N:\t\t Zoom in\n" +
                     "M:\t\t Zoom out\n",
                     "OK");
    }

    private void AboutButton_Clicked(object sender, EventArgs e)
    {
        DisplayAlert("About",
      "SnakeGame solution\nArtwork by Jolie Uk and Alex Smith\nGame design by Daniel Kopta and Travis Martin\n" +
      "Implementation by Olivia Bigelow & Michael Welsh\n" +
        "CS 3500 Fall 2022, University of Utah", "OK");
    }

    private void ContentPage_Focused(object sender, FocusEventArgs e)
    {
        if (!connectButton.IsEnabled)
            keyboardHack.Focus();
    }
}