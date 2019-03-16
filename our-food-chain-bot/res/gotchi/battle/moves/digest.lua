﻿function register(move)

	move.name = "Digest";
	move.description = "Attacks the opponent with digestive fluids. Has the chance to decrease all of the opponent's stats.";

	move.role = "decomposer";
	move.pp = 15;

	move.requires.role = "decomposer";
	move.requires.minLevel = 40;

end

function callback(args) 

	if(rand(0, 10) == 0) then
		args.target.stats.MultiplyAll(0.9);
	end;

	args.DoDamage();

end