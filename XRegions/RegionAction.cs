using System;

namespace XRegions
{
	/// <summary>
	/// Represents a region action.
	/// </summary>
	[Flags]
	public enum RegionAction
	{
		/// <summary>
		/// Does no action.
		/// </summary>
		None = 0,

		/// <summary>
		/// The force PvP action.
		/// </summary>
		ForcePvp = 1,

		/// <summary>
		/// The no PvP action.
		/// </summary>
		ForcePvpOff = 2,

		/// <summary>
		/// The heal action.
		/// </summary>
		Heal = 4,

		/// <summary>
		/// The item ban action.
		/// </summary>
		ItemBan = 8,

		/// <summary>
		/// The no mob action.
		/// </summary>
		NoMob = 16,

		/// <summary>
		/// The projectile ban.
		/// </summary>
		ProjectileBan = 32,

		/// <summary>
		/// The temp-group action.
		/// </summary>
		TempGroup = 64
	}
}