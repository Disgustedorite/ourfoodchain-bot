﻿function register(move)

	move.name = "helpless facade";
	move.description = "The user appears helpless and harmless, causing the opponent to let their guard down, lowering their DEF.";

	move.pp = 5;
	move.requires.match = "blind";

end

function callback(args) 

	args.target.def = args.target.def * 0.8;

end