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
using System.IO;
using System.Text;
using Chraft.PluginSystem;
using Chraft.PluginSystem.Commands;
using Chraft.PluginSystem.Net;
using Chraft.PluginSystem.World;
using Chraft.PluginSystem.Server;
using Chraft.Utilities.Coords;
using Chraft.Utilities.Misc;

namespace Chraft.Plugins.Schematics
{
    public class CmdSchematic : IClientCommand
    {
        protected SchematicsPlugin _plugin;

        public IPlugin Iplugin
        {
            get { return _plugin; }
            set { _plugin = value as SchematicsPlugin; }
        }

        public string Name { get { return "schematic"; } }

        public string Shortcut { get { return ""; } }

        public CommandType Type { get { return CommandType.Build; } }

        public string Permission { get { return "chraft.schematic"; } }

        public IClientCommandHandler ClientCommandHandler { get; set; }

        public CmdSchematic(IPlugin plugin)
        {
            Iplugin = plugin;
        }

        public void Help(IClient client)
        {
            client.SendMessage("/schematic list [pageNumber] - display a list of available schematics");
            client.SendMessage("/schematic place <schematic name> [x|z|xz] - place the specified schematic at current position and rotate it by X, Z or X & Z axis (optional)");
            client.SendMessage("/schematic info <schematic name> - display the info about specified schematic");
            client.SendMessage("/schematic undo - revert the changes made by the last schematic");
        }

        public string AutoComplete(IClient client, string str)
        {
            var args = new[] {"list", "place", "info", "undo"};
            if (string.IsNullOrEmpty(str.Trim()))
                return string.Join("\0", args);

            if (str.TrimStart().IndexOf(' ') != -1)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var a in args)
                if (a.StartsWith(str.Trim(), StringComparison.OrdinalIgnoreCase))
                    sb.Append(a).Append('\0');
            return sb.ToString();
        }

        public void Use(IClient client, string commandName, string[] tokens)
        {
            if (tokens.Length < 1)
            {
                Help(client);
                return;
            }

            switch (tokens[0].Trim().ToLower())
            {
                case "list":
                    uint page = 1;
                    if (tokens.Length >= 2 && !UInt32.TryParse(tokens[1], out page))
                        page = 1;
                    List(client, page);
                    return;
                case "place":
                    if (tokens.Length < 2)
                        break;
                    Place(client, tokens);
                    return;
                case "info":
                    if (tokens.Length < 2)
                        break;
                    Info(client, tokens[1]);
                    return;
                case "undo":
                    Undo(client);
                    return;
            }
            Help(client);
        }

        protected void List(IClient client, uint pageNumber)
        {
            int maxPerPage = 9;
            // 10 max
            if (!Directory.Exists(SchematicsPlugin.SchematicsFolder))
            {
                client.SendMessage("Schematics not found");
                return;
            }
            string[] files = Directory.GetFiles(SchematicsPlugin.SchematicsFolder, "*.schematic");

            if (files.Length == 0)
            {
                client.SendMessage("Schematics not found");
                return;
            }

            int totalPages = (files.Length / maxPerPage);
            if (files.Length % maxPerPage != 0)
                totalPages += 1;
            if (pageNumber < 1 || pageNumber > totalPages)
            {
                if (totalPages == 1)
                    client.SendMessage("Only page is available");
                else
                    client.SendMessage("Please specify the page number between 1 and " + totalPages);
                return;
            }

            client.SendMessage(string.Format("Schematics [{0}/{1}]:", pageNumber, totalPages));
            int startIndex = (int)(maxPerPage * (pageNumber - 1));
            int lastIndex = (pageNumber == totalPages ? files.Length : (int)(maxPerPage * pageNumber));
            for (int i = startIndex; i < lastIndex; i++)
            {
                string schematicName = files[i].Replace(".schematic", "").Replace(SchematicsPlugin.SchematicsFolder + Path.DirectorySeparatorChar, "");
                client.SendMessage(string.Format("{0}: {1}", (i + 1), schematicName));
            }
        }

        protected void Place(IClient client, string[] tokens)
        {
            string schematicName = tokens[1];
            Schematic schematic = new Schematic(schematicName);

            bool loaded;
            try
            {
                loaded = schematic.LoadFromFile();
            }
            catch (FileNotFoundException)
            {
                client.SendMessage(string.Format("Schematic file is not found: {0}", schematicName));
                return;
            }
            catch (Exception ex)
            {
                loaded = false;
                var sb = new StringBuilder();
                sb.Append("Schematics: error has occured while loading schematic file ");
                sb.Append(schematicName);
                sb.Append(Environment.NewLine);
                sb.Append(ex.ToString());
                client.GetServer().GetLogger().Log(LogLevel.Warning, sb.ToString());
            }

            if (!loaded)
            {
                client.SendMessage("Can not load schematic file");
                return;
            }

            bool rotateByX = false;
            bool rotateByZ = false;
            bool rotateByXZ = false;

            if (tokens.Length >= 3)
            {
                string rotation = tokens[2].Trim().ToLower();
                if (rotation == "x")
                    rotateByX = true;
                else if (rotation == "z")
                    rotateByZ = true;
                else if (rotation == "xz")
                    rotateByXZ = true;
            }

            UniversalCoords coords = UniversalCoords.FromAbsWorld(client.GetOwner().Position);
            int width = ((rotateByX || rotateByXZ) ? -1 * schematic.Width : schematic.Width);
            int length = ((rotateByZ || rotateByXZ) ? -1 * schematic.Length : schematic.Length);

            if (!RequiredChunksExist(client.GetOwner().GetWorld(), coords, width, schematic.Height, length))
            {
                client.SendMessage("The schematic is too big - required chunks are not loaded/created yet");
                return;
            }

            int blockAmount = schematic.Width * schematic.Height * schematic.Length;
            byte[] blockIds = new byte[blockAmount];
            byte[] blockMetas = new byte[blockAmount];
            UniversalCoords blockCoords;
            IChunk chunk;
            int index;

            for (int dx = 0; dx < schematic.Width; dx++)
                for (int dy = 0; dy < schematic.Height; dy++)
                    for (int dz = 0; dz < schematic.Length; dz++)
                    {
                        int x = coords.WorldX + ((rotateByX || rotateByXZ) ? -dx : dx);
                        int y = coords.WorldY + dy;
                        int z = coords.WorldZ + ((rotateByZ || rotateByXZ) ? -dz : dz);
                        blockCoords = UniversalCoords.FromWorld(x, y, z);
                        chunk = client.GetOwner().GetWorld().GetChunk(blockCoords, false, false);
                        if (chunk == null)
                            continue;
                        index = schematic.ToIndex(dx, dy, dz);
                        blockIds[index] = (byte)chunk.GetType(blockCoords);
                        blockMetas[index] = chunk.GetData(blockCoords);
                        chunk.SetBlockAndData(blockCoords, schematic.BlockIds[index], schematic.BlockMetas[index]);
                    }

            SchematicAction action;
            if (_plugin.Actions.ContainsKey(client.Username))
                _plugin.Actions.TryRemove(client.Username, out action);

            action = new SchematicAction
                         {
                             StartingPoint = coords,
                             BlockIds = blockIds,
                             BlockMetas = blockMetas,
                             RotateByX = rotateByX,
                             RotateByZ = rotateByZ,
                             RotateByXZ = rotateByXZ,
                             Width = schematic.Width,
                             Height = schematic.Height,
                             Length = schematic.Length
                         };
            _plugin.Actions.TryAdd(client.Username, action);
            client.SendMessage(string.Format("Schematic {0} ({1} blocks) has been placed", schematic.SchematicName, blockAmount));
        }

        protected bool RequiredChunksExist(IWorldManager world, UniversalCoords startingPoint, int xSize, int ySize, int zSize)
        {
            if (startingPoint.WorldY + ySize > 127)
                return false;
            UniversalCoords endPoint = UniversalCoords.FromWorld(startingPoint.WorldX + xSize, startingPoint.WorldY, startingPoint.WorldZ + zSize);
            if (startingPoint.ChunkX == endPoint.ChunkX && startingPoint.ChunkZ == endPoint.ChunkZ)
                return true;
            for (int x = startingPoint.ChunkX; x <= endPoint.ChunkX; x++)
                for (int z = startingPoint.ChunkZ; z <= endPoint.ChunkZ; z++)
                    if (world.GetChunkFromChunkSync(x, z) == null)
                        return false;
            return true;
        }

        protected void Info(IClient client, string schematicName)
        {
            Schematic schematic = new Schematic(schematicName);

            bool loaded;
            try
            {
                loaded = schematic.LoadFromFile(true);
            }
            catch (FileNotFoundException)
            {
                client.SendMessage(string.Format("Schematic file is not found: {0}", schematicName));
                return;
            }
            catch (Exception ex)
            {
                loaded = false;
                var sb = new StringBuilder();
                sb.Append("Schematics: error has occured while loading schematic file ");
                sb.Append(schematicName);
                sb.Append(Environment.NewLine);
                sb.Append(ex.ToString());
                client.GetServer().GetLogger().Log(LogLevel.Warning, sb.ToString());
            }

            if (!loaded)
            {
                client.SendMessage("Can not load schematic file");
                return;
            }

            client.SendMessage(string.Format("Width(X) x Height(Y) x Length(Z): {0} x {1} x {2} ({3} blocks)", schematic.Width, schematic.Height, schematic.Length, (schematic.Width * schematic.Height * schematic.Length)));
        }

        protected void Undo(IClient client)
        {
            if (!_plugin.Actions.ContainsKey(client.Username))
            {
                client.SendMessage("No changes were made by you");
                return;
            }
            SchematicAction action;
            if (!_plugin.Actions.TryGetValue(client.Username, out action))
            {
                client.SendMessage("Can not revert the changes");
                return;
            }

            UniversalCoords coords = action.StartingPoint;
            bool rotateByX = action.RotateByX;
            bool rotateByZ = action.RotateByZ;
            bool rotateByXZ = action.RotateByXZ;
            int width = ((rotateByX || rotateByXZ) ? -1 * action.Width : action.Width);
            int length = ((rotateByZ || rotateByXZ) ? -1 * action.Length : action.Length);

            if (!RequiredChunksExist(client.GetOwner().GetWorld(), coords, width, action.Height, length))
            {
                client.SendMessage("Can not revert the changes - required chunks are not loaded");
                return;
            }

            UniversalCoords blockCoords;
            IChunk chunk;
            for (int dx = 0; dx < action.Width; dx++)
                for (int dy = 0; dy < action.Height; dy++)
                    for (int dz = 0; dz < action.Length; dz++)
                    {
                        int x = coords.WorldX + ((rotateByX || rotateByXZ) ? -dx : dx);
                        int y = coords.WorldY + dy;
                        int z = coords.WorldZ + ((rotateByZ || rotateByXZ) ? -dz : dz);
                        blockCoords = UniversalCoords.FromWorld(x, y, z);
                        chunk = client.GetOwner().GetWorld().GetChunk(blockCoords);
                        if (chunk == null)
                            continue;
                        int index = action.Width*(dy*action.Length + dz) + dx;
                        chunk.SetBlockAndData(blockCoords, action.BlockIds[index], action.BlockMetas[index]);
                    }
            client.SendMessage(string.Format("Schematic placement has been successfully reverted ({0} blocks)", action.Width * action.Height * action.Length));
            _plugin.Actions.TryRemove(client.Username, out action);
        }
    }
}
