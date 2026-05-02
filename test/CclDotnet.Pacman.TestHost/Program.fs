open CatConfLang.TestRunner
open CclDotnet.Pacman

CclTestHost.Run(System.Environment.GetCommandLineArgs()[1..], CclPacmanImplementation())
|> exit
