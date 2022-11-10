# Budget Tetris
This is a little personal project of mine. It's a Tetris clone emulated on a console terminal.
That was the purpose at first, but I'm also really into AI so I later decided to create reinforcement learning models to play the game and maximise score.
As I fell deeper into the Tetris rabbit hole, my focus shifted to multiplayer Tetris and after adding multiplayer functionality, I trained new models to maximise attack.
I noticed that a number of people are interested in playing this, so I have decided to put effort into playablity and UI, and to give players the ability to play alone or with any number of bots.

## Pre-requisites
Since the app requires Windows Forms for capturing input and playing audio, this only runs on Windows.
This also requires the .NET 4.6 runtime, which already comes pre-installed on mordern Windows versions. However, if you do not have it, you can install it [here](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net46-web-installer).

## Controls [^1]
- ***Left/Right arrow keys***: Shift left/right. DAS is supported.
- ***Up arrow/X key***: Rotate 90 degrees clockwise.
- ***Z key***: Rotate 90 degrees counter-clockwise
- ***A key***: Rotate 180 degrees.
- ***Down arrow key***: Soft drop.
- ***Space key***: Hard drop.
- ***C key***: Hold piece. Once a piece is held, the new piece must be placed before the player can hold again.
- ***R key***: Restart game.
- ***Escape key***: Pause/Unpause game.
- ***M key***: Mute/Unmute.

DAS settings may be changed in the "Settings.json" file.

## Settings
The settings can be found and edited in the "Settings.json" file.
Deleting the file will cause the app to generate a new one with the default settings.
Below is an explaination of the different settings avaliable.

#### BGMVolume
The volume of background music.
The accepted range is from 0.0 to 1.0 (0% to 100%).

#### SFXVolume
The volume of sound effects.
The accepted range is from 0.0 to 1.0 (0% to 100%).

#### LinesTrash
The number of lines of trash to send for normal line clears.
It is an array consisting of 5 positive whole numbers.
The first element represents how much trash to send if no lines are cleared, the second if 1 line is cleared, the third if 2 lines are cleared, etc.

#### TSpinTrash
The number of lines of trash to send for t-spin clears.
It is an array consisting of 4 positive whole numbers.
The first element represents how much trash to send if no lines are cleared, the second if 1 line is cleared (aka TSS), the third if 2 lines are cleared (aka TSD), etc.

#### ComboTrash
The number of lines of trash to send based on the current combo.
It is an array of any number of positive whole numbers.
The first element represents how much trash to send for a 1-combo, the second a 2-combo, the third a 3-combo, etc.
If the current combo exceeds the length of the array, the last number in the array is used.

#### PCTrash
The number of lines of trash to send for perfect clears.
It is an array consisting of 4 positive whole numbers.
The first element represents how much trash to send if 1 line is cleared, the second if 2 lines are cleared, the third if 3 lines are cleared, etc.[^2]

#### G
The gravity.
It is a positive rational number that represents how many lines the current piece will move down by each frame.
A value of 20.0 or more will cause pieces to instantly drop down.
Check footnotes for framerate.[^3]

#### SoftG
The soft drop gravity.
It is a positive rational number that represents the amount of gravity *added* when the soft drop key is pressed.
A value of 20.0 or more will cause pieces to instantly drop down.

#### LookAheads
The number of lookaheads avaliable to all players.
If it is negative or 0, the lookaheads will be hidden, but bots will still have 1 lookahead.
It must be a whole number.

#### DASDelay
The delay, in milliseconds, after which DAS will start, when the shift left/right key is held down.
A negative value will disable DAS.
It must be a whole number.

#### DASInterval
The delay, in milliseconds, between movements caused by DAS, when the shift left/right key is held down.
A value shorter than half the length of 1 frame will cause DAS to move the piece to either side instantaneously.
It must be a positive whole number.

#### LockDelay
The delay, in milliseconds, after which a piece will lock after touching the ground without moving (provided *AutoLockGrace* has not been exceeded).
It must be a positive whole number.

#### EraseDelay
The delay, in milliseconds, after which the stats of the previous clear will be erased from the screen.
If a new clear is done within the delay, the stats of the previous clear are overwritten and the delay is reset.
It must be a positive whole number.

#### GarbageDelay
The delay, in milliseconds, after which garbage received will be dumped into the player's matrix (aka board/playing field).
It must be a positive whole number.

#### AutoLockGrace
The number of movements that can be made before further movements do not reset the lock delay.
Placing or holding a piece resets the counter.
It must be a positive whole number.

#### TargetChangeInteval
The delay, in milliseconds, after which the current targets will be changed.
This setting only affects the *Random* targetting mode.[^4]
It must be a positive whole number.

## Configuring the game and bots
The game configurations can be found and edited in the "Config.json" file.

#### HasPlayer
Whether or not to include an extra game for a human player.
This value should only be "true" or "false".

#### Bots
An array of bots to include in the game.
If empty, the game will only include the human player.
The configuration of each bot is explained below.

##### NNPath
The path of the file containing the neural network of the bot.
You can find some of pre-trained models in the "NNs" folder.
To use these, set *NNPath* to "NNs/model name.json".

##### ThinkTime
The amount of time, in milliseconds, that the bot will think before making a move.
It must be a positive whole number.

##### MoveDelay
The delay, in milliseconds, between each of the bot's inputs.
The bot will not think while making a move.
It must be a positive whole number.

An example of a configuration with 2 bots is shown below.
```json
{
  "HasPlayer": true,
  "Bots": [
	{
	  "NNPath": "NNs/plan2.json",
	  "ThinkTime": 1000,
	  "MoveDelay": 100
	},
	{
	  "NNPath": "NNs/Zhodenifi.json",
	  "ThinkTime": 100,
	  "MoveDelay": 300
	}
  ]
}
```


[^1]: I have plans to make the keybinds customisable in the future.
[^2]: Note you cannot do a perfect clear without clearing any lines, even though clearing 0 lines can still technically be considered a line clear.
[^3]: The app runs at 30 frames per second. However, inputs are processed 2000 times per second, to ensure millisecond accuracy. I may consider adding an adjustable framerate in the future.
[^4]: The *Random* targetting mode is used by default and currently there is no way of changing it.
