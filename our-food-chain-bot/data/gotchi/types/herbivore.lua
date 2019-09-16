﻿function register(type)

	-- Herbivores are well-rounded.

	type.SetName("herbivore")
	type.SetColor(0, 0, 255)

	type.SetBaseHp(44)
	type.SetBaseAtk(49)
	type.SetBaseDef(49)
	type.SetBaseSpd(45)

	type.SetMatchup("autotroph", 1.5)

	type.Requires.RoleMatch("base-consumer|herbivore")

end