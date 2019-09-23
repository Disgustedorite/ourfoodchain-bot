﻿function OnRegister(move)

	move.SetName("overgrowth")
	move.SetDescription("Accelerates growth, boosting attack by a moderate amount.")
	move.SetType("producer")

	move.SetPP(25)

	move.Requires.TypeMatch("producer")
	move.Requires.MinimumLevel(30)

end

function OnMove(args) 	
	
	args.User.Stats.Atk = args.User.Stats.Atk * 1.2

end