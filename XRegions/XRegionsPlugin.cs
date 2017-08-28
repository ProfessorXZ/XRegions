using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Localization;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using XRegions.Database;

namespace XRegions
{
	/// <summary>
	/// Represents the XRegions plugin.
	/// </summary>
	[ApiVersion(2, 1)]
	public sealed class XRegionsPlugin : TerrariaPlugin
	{
		private XRegionManager _manager;

		/// <summary>
		/// Initializes a new instance of the <see cref="XRegionsPlugin"/> class with the specified <see cref="Main"/> instance.
		/// </summary>
		/// <param name="game">The <see cref="Main"/> instance.</param>
		public XRegionsPlugin(Main game) : base(game)
		{
		}

		/// <inheritdoc />
		public override string Author => "Professor X";

		/// <inheritdoc />
		public override string Description => "Provides an advanced region management system.";

		/// <inheritdoc />
		public override string Name => "XRegions";

		/// <inheritdoc />
		public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				_manager.Dispose();

				RegionHooks.RegionDeleted -= OnRegionDeleted;
				RegionHooks.RegionEntered -= OnRegionEntered;
				RegionHooks.RegionLeft -= OnRegionLeft;
				ServerApi.Hooks.GamePostInitialize.Deregister(this, OnGamePostInitialize);
				ServerApi.Hooks.NetGetData.Deregister(this, OnNetGetData);
				ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);

				Commands.ChatCommands.RemoveAll(c => c.CommandDelegate == XRegionCommands);
			}

			base.Dispose(disposing);
		}

		/// <inheritdoc />
		public override void Initialize()
		{
			_manager = new XRegionManager();

			RegionHooks.RegionDeleted += OnRegionDeleted;
			RegionHooks.RegionEntered += OnRegionEntered;
			RegionHooks.RegionLeft += OnRegionLeft;
			ServerApi.Hooks.GamePostInitialize.Register(this, OnGamePostInitialize, -1);
			ServerApi.Hooks.NetGetData.Register(this, OnNetGetData);
			ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);

			Commands.ChatCommands.Add(new Command(XRegionCommands, "xregion"));
		}

		private void OnGamePostInitialize(EventArgs e)
		{
			_manager.Load();
		}

		private void OnNetGetData(GetDataEventArgs e)
		{
			if (e.Handled)
			{
				return;
			}

			using (var stream = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
			using (var reader = new BinaryReader(stream))
			{
				switch (e.MsgID)
				{
					case PacketTypes.TogglePvp:
						OnPlayerTogglePvp(reader);
						break;
				}
			}
		}

		private void OnPlayerTogglePvp(BinaryReader reader)
		{
			var playerId = reader.ReadByte();
			var pvp = reader.ReadBoolean();

			var player = TShock.Players[playerId];
			if (player.CurrentRegion == null)
			{
				return;
			}

			var region = _manager.Get(player.CurrentRegion.Name);
			if (region == null)
			{
				return;
			}

			if (!pvp && region.HasAction(RegionAction.ForcePvp))
			{
				player.TPlayer.hostile = true;
				NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, NetworkText.Empty, player.Index);
				player.SendInfoMessage("You are in a PvP area, your PvP status is forced.");
			}
		}

		private void OnRegionDeleted(RegionHooks.RegionDeletedEventArgs e)
		{
			var region = _manager.Get(e.Region.Name);
			if (region == null)
			{
				return;
			}

			_manager.Remove(e.Region.Name);
			TShock.Log.ConsoleInfo($"Region '{e.Region.Name}' has been removed from the XRegions database.");
		}

		private void OnRegionEntered(RegionHooks.RegionEnteredEventArgs e)
		{
			var player = e.Player;
			var playerInfo = player.GetOrCreatePlayerInfo();
			var region = _manager.Get(e.Region.Name);
			if (region == null)
			{
				return;
			}

			if (!player.TPlayer.hostile && region.HasAction(RegionAction.ForcePvp))
			{
				player.TPlayer.hostile = true;
				NetMessage.SendData((int) PacketTypes.TogglePvp, -1, -1, NetworkText.Empty, player.Index);
				player.SendInfoMessage("You have entered a PvP area, your PvP is now forced.");
			}

			if (region.HasAction(RegionAction.TempGroup) && region.TempGroup != null)
			{
				playerInfo.PreviousGroup = player.Group;
				player.Group = region.TempGroup;
				player.SendInfoMessage(
					$"Your group has been changed to '{TShock.Utils.ColorTag(region.TempGroup.Name, new Color(region.TempGroup.R, region.TempGroup.G, region.TempGroup.B))}' due to region setup.");
			}
		}

		private void OnRegionLeft(RegionHooks.RegionLeftEventArgs e)
		{
			var player = e.Player;
			var playerInfo = player.GetOrCreatePlayerInfo();
			var region = _manager.Get(e.Region.Name);
			if (region == null || !region.HasAction(RegionAction.TempGroup))
			{
				return;
			}

			player.Group = playerInfo.PreviousGroup;
			player.SendInfoMessage("Your group has been reverted to default.");
		}

		private static void OnServerLeave(LeaveEventArgs e)
		{
			var player = TShock.Players[e.Who];
			if (player == null)
			{
				return;
			}

			var playerInfo = player.GetOrCreatePlayerInfo();
			if (player.Group != playerInfo.PreviousGroup)
			{
				player.Group = playerInfo.PreviousGroup;
			}
		}

		private void XRegionCommands(CommandArgs e)
		{
			var player = e.Player;
			if (e.Parameters.Count < 1)
			{
				player.SendErrorMessage($"Invalid syntax! Use {Commands.Specifier}xregion help for help.");
				return;
			}

			switch (e.Parameters[0].ToLowerInvariant())
			{
				case "addflag":
				{
					if (!player.HasPermission(XRegionsPermissions.ModifyActions))
					{
						player.SendErrorMessage("You do not have permission to modify a region's actions.");
						return;
					}

					if (e.Parameters.Count != 3)
					{
						player.SendErrorMessage($"Invalid syntax! Proper syntax: {Commands.Specifier}xregion addflag <region name> <flag>");
						return;
					}

					var flag = e.Parameters[2];
					var regionName = e.Parameters[1];
					var region = _manager.Get(regionName);
					if (region == null)
					{
						player.SendErrorMessage($"No regions under the name of '{regionName}'.");
						return;
					}

					if (!Enum.TryParse<RegionAction>(flag, out var action))
					{
						player.SendErrorMessage(
							$"Invalid action! Valid actions: {string.Join(", ", Enum.GetNames(typeof(RegionAction)))}");
						return;
					}

					region.Actions.Add(action);
					_manager.Update(region);
					player.SendSuccessMessage($"Region '{regionName}' now has action '{action}'.");
				}
					break;
				case "define":
				{
					if (!player.HasPermission(XRegionsPermissions.DefineRegions))
					{
						player.SendErrorMessage("You do not have permission to define regions.");
						return;
					}

					if (e.Parameters.Count != 2)
					{
						player.SendErrorMessage($"Invalid syntax! Proper syntax: {Commands.Specifier}xregion define <region name>");
						return;
					}

					var regionName = e.Parameters[1];
					var region = TShock.Regions.GetRegionByName(regionName);
					if (region == null)
					{
						player.SendErrorMessage($"No regions under the name of '{regionName}'.");
						return;
					}

					if (_manager.Get(region.Name) != null)
					{
						player.SendErrorMessage("This region is already defined as an XRegion.");
						return;
					}

					_manager.Add(region.Name);
					player.SendInfoMessage($"Region '{region.Name}' has been defined as an XRegion.");
				}
					break;
				case "deleteflag":
				{
					if (!player.HasPermission(XRegionsPermissions.ModifyActions))
					{
						player.SendErrorMessage("You do not have permission to modify a region's actions.");
						return;
					}

					if (e.Parameters.Count != 3)
					{
						player.SendErrorMessage($"Invalid syntax! Proper syntax: {Commands.Specifier}xregion deleteflag <region name> <flag>");
						return;
					}

					var flag = e.Parameters[2];
					var regionName = e.Parameters[1];
					var region = _manager.Get(regionName);
					if (region == null)
					{
						player.SendErrorMessage($"No regions under the name of '{regionName}'.");
						return;
					}

					if (!Enum.TryParse<RegionAction>(flag, out var action))
					{
						player.SendErrorMessage(
							$"Invalid action! Valid actions: {string.Join(", ", Enum.GetNames(typeof(RegionAction)))}");
						return;
					}

					region.Actions.Remove(action);
					_manager.Update(region);
					player.SendSuccessMessage($"Region '{regionName}' no longer has action '{action}'.");
				}
					break;
				case "list":
				{
					player.SendInfoMessage(
						$"Defined XRegions: {string.Join(", ", from region in _manager.GetRegions() where region.Region != null orderby region.Region.Name select region.Region.Name)}");
				}
					break;
				case "listactions":
				{
					if (e.Parameters.Count != 2)
					{
						e.Player.SendErrorMessage($"Invalid synax! Proper syntax: {Commands.Specifier}xregion listactions <region name>");
						return;
					}

					var regionName = e.Parameters[1];
					var region = _manager.Get(regionName);
					if (region == null)
					{
						player.SendErrorMessage($"No regions under the name of '{regionName}'.");
						return;
					}

					player.SendInfoMessage(
						$"Region '{regionName}' contains the following actions: {string.Join(", ", region.Actions)}");
				}
					break;
				case "setgroup":
				{
					if (!player.HasPermission(XRegionsPermissions.SetRegionGroup))
					{
						player.SendErrorMessage("You do not have permission to modify a region's group.");
						return;
					}

					if (e.Parameters.Count != 3)
					{
						player.SendErrorMessage($"Invalid syntax! Proper syntax: {Commands.Specifier}xregion setgroup <region name> <group name>");
						return;
					}

					var groupName = e.Parameters[2];
					var regionName = e.Parameters[1];
					var region = _manager.Get(regionName);
					if (region == null)
					{
						player.SendErrorMessage($"No regions under the name of '{regionName}'.");
						return;
					}

					var group = TShock.Groups.GetGroupByName(groupName);
					if (group == null)
					{
						player.SendErrorMessage($"No groups under the name of '{groupName}'.");
						return;
					}

					region.TempGroup = group;
					_manager.Update(region);
					player.SendSuccessMessage($"Region '{regionName}' now references group '{groupName}'.");
				}
					break;
			}
		}
	}
}