using TShockAPI;

namespace XRegions
{
	/// <summary>
	/// Holds extension methods.
	/// </summary>
	public static class Extensions
	{
		private const string PlayerInfoKey = "XRegions_PlayerInfo";

		/// <summary>
		/// Gets the player's info.
		/// </summary>
		/// <param name="player">The player.</param>
		/// <returns>The player info.</returns>
		public static PlayerInfo GetOrCreatePlayerInfo(this TSPlayer player)
		{
			var playerInfo = player.GetData<PlayerInfo>(PlayerInfoKey);
			if (playerInfo == null)
			{
				playerInfo = new PlayerInfo {PreviousGroup = player.Group};
				player.SetData(PlayerInfoKey, playerInfo);
			}

			return playerInfo;
		}
	}
}
