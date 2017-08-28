using System.Collections.Generic;
using TShockAPI;
using TShockAPI.DB;

namespace XRegions.Database
{
	/// <summary>
	/// Represents an XRegion.
	/// </summary>
	public sealed class XRegion
	{
		/// <summary>
		/// Gets the actions.
		/// </summary>
		public List<RegionAction> Actions { get; }

		/// <summary>
		/// Gets the TShock region.
		/// </summary>
		public Region Region { get; }

		/// <summary>
		/// Gets the temp group.
		/// </summary>
		public Group TempGroup { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="XRegion"/> class with the specified region name and actions.
		/// </summary>
		/// <param name="region">The region name.</param>
		/// <param name="actions">The actions.</param>
		public XRegion(string region, List<RegionAction> actions)
		{
			Region = TShock.Regions.GetRegionByName(region);
			Actions = actions;
		}

		/// <summary>
		/// Determines whether the region contains an action.
		/// </summary>
		/// <param name="action">The action.</param>
		/// <returns>True or false.</returns>
		public bool HasAction(RegionAction action)
		{
			return Actions.Contains(action);
		}
	}
}