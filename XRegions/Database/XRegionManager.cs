using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using TShockAPI;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI.DB;

namespace XRegions.Database
{
	/// <summary>
	/// Represents the XRegion database manager.
	/// </summary>
	public sealed class XRegionManager : IDisposable
	{
		private readonly IDbConnection _dbConnection;
		private readonly List<XRegion> _regions = new List<XRegion>();

		/// <summary>
		/// Initializes a new instance of the <see cref="XRegionManager"/> class.
		/// </summary>
		public XRegionManager()
		{
			switch (TShock.Config.StorageType.ToLower())
			{
				case "mysql":
					var dbHost = TShock.Config.MySqlHost.Split(':');
					_dbConnection = new MySqlConnection
					{
						ConnectionString =
							$"Server={dbHost[0]}; Port={(dbHost.Length == 1 ? "3306" : dbHost[1])}; Database={TShock.Config.MySqlDbName}; Uid={TShock.Config.MySqlUsername}; Pwd={TShock.Config.MySqlPassword};"
					};
					break;

				case "sqlite":
					var path = Path.Combine(TShock.SavePath, "tshock.sqlite");
					_dbConnection = new SqliteConnection($"uri=file://{path},Version=3");
					break;
			}

			var tableCreator = new SqlTableCreator(_dbConnection,
				_dbConnection.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder) new SqliteQueryCreator() : new MysqlQueryCreator());
			tableCreator.EnsureTableStructure(new SqlTable("XRegions", new SqlColumn("Name", MySqlDbType.Text),
				new SqlColumn("Actions", MySqlDbType.Text), new SqlColumn("TempGroup", MySqlDbType.Text)));
			tableCreator.EnsureTableStructure(new SqlTable("XRegionBans", new SqlColumn("Name", MySqlDbType.Text),
				new SqlColumn("ItemBans", MySqlDbType.Text), new SqlColumn("ProjectileBans", MySqlDbType.Text)));
		}

		/// <summary>
		/// Disposes the connection.
		/// </summary>
		public void Dispose()
		{
			_dbConnection.Dispose();
		}

		/// <summary>
		/// Inserts a region with the specified name.
		/// </summary>
		/// <param name="name">The name.</param>
		public void Add(string name)
		{
			var region = new XRegion(name, new List<RegionAction>());
			_regions.Add(region);
			_dbConnection.Query("INSERT INTO XRegions (Name, Actions, TempGroup) VALUES (@0, @1, @2)", name, string.Empty,
				string.Empty);
		}

		/// <summary>
		/// Gets a region with the specified name.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns>The region.</returns>
		public XRegion Get(string name)
		{
			return _regions.FirstOrDefault(r => r.Region?.Name.Equals(name, StringComparison.CurrentCulture) ?? false);
		}

		/// <summary>
		/// Returns an enumerable collection of defined regions.
		/// </summary>
		/// <returns>The defined regions.</returns>
		public IEnumerable<XRegion> GetRegions()
		{
			return _regions.AsReadOnly();
		}

		/// <summary>
		/// Loads the regions.
		/// </summary>
		public void Load()
		{
			_regions.Clear();
			using (var reader = _dbConnection.QueryReader("SELECT * FROM XRegions"))
			{
				while (reader.Read())
				{
					var name = reader.Get<string>("Name");
					var actions = new List<RegionAction>();
					var tempGroup = TShock.Groups.GetGroupByName(reader.Get<string>("TempGroup") ?? "");
					foreach (var actionName in reader.Get<string>("Actions").Split(','))
					{
						if (Enum.TryParse<RegionAction>(actionName, out var action))
						{
							actions.Add(action);
						}
					}

					var region = new XRegion(name, actions) {TempGroup = tempGroup};
					using (var banReader = _dbConnection.QueryReader("SELECT * FROM XRegionBans WHERE Name = @0", name))
					{
						if (banReader.Read())
						{
							foreach (var itemBan in banReader.Get<string>("ItemBans").Split(','))
							{
								if (int.TryParse(itemBan, out var itemId))
								{
									region.BannedItems.Add(itemId);
								}
							}
							foreach (var projectileBan in banReader.Get<string>("ProjectileBans").Split(','))
							{
								if (int.TryParse(projectileBan, out var projectileId))
								{
									region.BannedProjectiles.Add(projectileId);
								}
							}
						}
					}
					_regions.Add(region);
				}
			}
		}

		/// <summary>
		/// Removes a region with the specified name.
		/// </summary>
		/// <param name="name">The name.</param>
		public void Remove(string name)
		{
			_dbConnection.Query("DELETE FROM XRegions WHERE Name = @0", name);
			_dbConnection.Query("DELETE FROM XRegionBans WHERE Name = @0", name);
			_regions.RemoveAll(r => r.Region.Name == name);
		}

		/// <summary>
		/// Updates a region.
		/// </summary>
		/// <param name="region">The region.</param>
		public void Update(XRegion region)
		{
			_dbConnection.Query("UPDATE XRegions SET Actions = @0, TempGroup = @1 WHERE Name = @2",
				string.Join(",", region.Actions), region.TempGroup?.Name ?? string.Empty, region.Region.Name);

			_dbConnection.Query("DELETE FROM XRegionBans WHERE Name = @0", region.Region.Name);
			_dbConnection.Query("INSERT INTO XRegionBans (Name, ItemBans, ProjectileBans) VALUES (@0, @1, @2)",
				region.Region.Name, string.Join(",", region.BannedItems), string.Join(",", region.BannedProjectiles));
		}
	}
}