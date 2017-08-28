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
		/// The heal action.
		/// </summary>
		Heal = 2,

		/// <summary>
		/// The temp-group action.
		/// </summary>
		TempGroup = 4
	}
}