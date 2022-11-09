# Budget Tetris
This is a little personal project of mine. It's a Tetris clone emulated on a console terminal.
That was the purpose at first, but I'm also really into AI so I later decided to create reinforcement learning models to play the game and maximise score.
As I fell deeper into the Tetris rabbit hole, my focus shifted to multiplayer Tetris and after adding multiplayer functionality, I trained new models to maximise attack.
I noticed that a number of people are interested in playing this, so I have decided to put effort into playablity and UI, and to give players the ability to play alone or with any number of bots.

## Pre-requisites
Since the app requires Windows Forms for capturing input and playing audio, this only runs on Windows.
This also requires the .NET 4.6 runtime, which already comes pre-installed on mordern Windows versions. However, if you do not have it, you can install it [here](https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net46-web-installer).

## Control [^1]
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

## Settings
The settings can be found and edited in the "Settings.json" file.
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
The first element represents how much trash to send if 1 line is cleared, the second if 2 lines are cleared, the third if 3 lines are cleared, etc.[^PCTrash]

#### G
The gravity.
It is a positive rational number that represents how many lines the current piece will move down by each frame.
A value of 20.0 or more will cause pieces to instantly drop down.
Check footnotes for framerate.

#### SoftG
The soft drop gravity.
It is a positive rational number that represents the amount of gravity *added* when the soft drop key is pressed.
A value of 20.0 or more will cause pieces to instantly drop down.

#### LockDelay
The delay, in milliseconds, after which a piece will lock after touching the ground without moving (provided [AutoLockGrace](https://github.com/Zemogus/Budget-Tetris/tree/battle-bots-net4.6#autolockgrace) has not been exceeded).
It must be a positive whole number.

#### EraseDelay
The delay, in milliseconds, after which the stats of the previous clear will be erased from the screen.
It must be a positive whole number.
If a new clear is done within the delay, the stats of the previous clear are overwritten and the delay is reset.

#### GarbageDelay
The delay, in milliseconds, after which garbage received will be dumped into the player's matrix (aka board/playing field).
It must be a positive whole number.

#### AutoLockGrace
The number of movements that can be made before further movements do not reset the lock delay.
Placing or holding a piece resets the counter.
It must be a positive whole number.

#### TargetChangeInteval
The delay, in milliseconds, after which

## Setting up bots


[^1]: I have plans to make the keybinds customisable in the future.
[^PCTrash]: Note you cannot do a perfect clear without clearing any lines, even though clearing 0 lines can still technically be considered a line clear.
