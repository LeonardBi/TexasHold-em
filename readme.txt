=================================================================
TEXAS HOLD'EM MULTIPLAYER GAME - INSTALLATION & USER MANUAL
=================================================================

A functional, real-time multiplayer Texas Hold'em application built with C#, WPF, and .NET 8.0. This project utilizes an Authoritative Server architecture powered by Microsoft SignalR to ensure game state synchronization and prevent client-side cheating.

-----------------------------------------------------------------
1. SYSTEM PREREQUISITES
-----------------------------------------------------------------
Before running the application, ensure you have the following installed:
* IDE: Visual Studio 2022
* SDK: .NET 8.0 SDK
* Database: MySQL Server (v8.0 or higher) and MySQL Workbench

-----------------------------------------------------------------
2. DATABASE SETUP (CRUCIAL FIRST STEP)
-----------------------------------------------------------------
The system requires a local MySQL database for user authentication and chip persistence.
1) Open MySQL Workbench.
2) Open and execute the script: 'Database_Setup.sql' (found in this root directory). This will automatically create the database schema and test accounts.
3) In the 'TexasHoldemServer' project, open 'appsettings.json'.
4) Update the 'DefaultConnection' string to match your local MySQL 'Uid' (Username) and 'Pwd' (Password).

-----------------------------------------------------------------
3. RUNNING THE SYSTEM
-----------------------------------------------------------------
To simulate multiplayer gameplay on a single machine:

Step A: Start the Server
1) Open 'TexasHoldem.sln' in Visual Studio.
2) Right-click 'TexasHoldemServer' and set it as the Startup Project.
3) Press F5. A console window will open indicating the server is listening. (Do not close this window).

Step B: Start the Client(s)
1) Right-click 'TexasHoldemClient' and set it as the Startup Project.
2) Press F5 to launch the first player's game window.
3) To add more players (multiplayer simulation), navigate to the folder:
  \TexasHolem\TexasHolem\bin\Debug\net8.0-windows\
   and double-click 'TexasHoldemClient.exe' to open additional windows.

-----------------------------------------------------------------
4. GAMEPLAY & FEATURES
-----------------------------------------------------------------
Test Accounts:
You can log in using these pre-configured test accounts:
* User 1: Username: test02 | Password: test02
* User 2: Username: test03 | Password: test03
(Alternatively, use the "Sign Up" button to create a new local account).

The "River Swap" Mechanic:
After the final community card (the River) is revealed:
1) The game enters the "River Swap" phase.
2) Click on one of your hole cards to select it.
3) Click "Swap Selected" to exchange it for a new card from the deck, or "Keep Hand" to proceed.
4) The game then moves to the Showdown to determine the winner.

=================================================================