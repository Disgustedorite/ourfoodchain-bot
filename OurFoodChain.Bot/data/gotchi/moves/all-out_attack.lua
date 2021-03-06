﻿function OnRegister(move)
	
	move.SetName("all-out attack")
	move.SetDescription("Rushes the opponent. Has abysmal accuracy, but deals very high damage.")
	move.SetType("predator")

	move.SetPower(80)
	move.SetPP(10)
	move.SetAccuracy(0.1)

	move.Requires.TypeMatch("predator")
	move.Requires.MinimumLevel(20)

end