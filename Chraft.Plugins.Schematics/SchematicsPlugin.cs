#region C#raft License
// This file is part of C#raft. Copyright C#raft Team 
// 
// C#raft is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License
// along with this program. If not, see <http://www.gnu.org/licenses/>.
#endregion
using System;
using System.Collections.Concurrent;
using System.Reflection;
using Chraft.PluginSystem;
using Chraft.PluginSystem.Commands;
using Chraft.PluginSystem.Event;
using Chraft.PluginSystem.Server;
using Chraft.Utilities.Coords;

namespace Chraft.Plugins.Schematics
{
    [Plugin]
    public class SchematicsPlugin : IPlugin
    {
        private SchematicsPluginPlayerListener _playerListener;

        public string Name { get { return "Schematics"; } }

        public string Author { get { return "C#raft Team"; } }

        public string Description { get { return "Adds an ability to place the schematics in the world"; } }

        public string Website { get { return "http://www.c-raft.com"; } }

        public Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }

        public IServer Server { get; set; }

        public IPluginManager PluginManager { get; set; }

        public bool IsPluginEnabled { get; set; }

        public ConcurrentDictionary<string, SchematicAction> Actions;

        public static string SchematicsFolder { get { return "Schematics"; } }

        protected ICommand SchematicCommand;

        public void Initialize()
        {
            _playerListener = new SchematicsPluginPlayerListener(this);
            Actions = new ConcurrentDictionary<string, SchematicAction>();
            SchematicCommand = new CmdSchematic(this);
        }

        public void Associate(IServer server, IPluginManager pluginManager)
        {
            Server = server;
            PluginManager = pluginManager;
        }

        public void OnEnabled()
        {
            IsPluginEnabled = true;
            PluginManager.RegisterEvent(Event.PlayerLeft, _playerListener, this);
            PluginManager.RegisterEvent(Event.PlayerKicked, _playerListener, this);
            PluginManager.RegisterCommand(SchematicCommand, this);
            Server.GetLogger().Log(LogLevel.Info, "Plugin {0} v{1} Enabled", Name, Version);
        }

        public void OnDisabled()
        {
            IsPluginEnabled = false;
            PluginManager.UnregisterEvent(Event.PlayerLeft, _playerListener, this);
            PluginManager.UnregisterEvent(Event.PlayerKicked, _playerListener, this);
            PluginManager.UnregisterCommand(SchematicCommand, this);
            Server.GetLogger().Log(LogLevel.Info, "Plugin {0} v{1} Disabled", Name, Version);
        }
    }

    public struct SchematicAction
    {
        public UniversalCoords StartingPoint;
        public bool RotateByX;
        public bool RotateByZ;
        public bool RotateByXZ;
        public byte[] BlockIds;
        public byte[] BlockMetas;
        public int Width;
        public int Height;
        public int Length;
    }
}
