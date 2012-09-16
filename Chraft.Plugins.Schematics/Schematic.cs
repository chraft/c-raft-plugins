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
using System.Collections.Generic;
using System.IO;
using Chraft.Utilities.NBT;

namespace Chraft.Plugins.Schematics
{
    public class Schematic
    {
        /// <summary>
        /// Schematic name: file name without extension
        /// </summary>
        public string SchematicName { get; protected set; }

        /// <summary>
        /// Y-size
        /// </summary>
        public int Height { get; protected set; }

        /// <summary>
        /// Z-size
        /// </summary>
        public int Length { get; protected set; }

        /// <summary>
        /// X-size
        /// </summary>
        public int Width { get; protected set; }

        /// <summary>
        /// Level format - Alpha or Classic
        /// </summary>
        public string Level { get; protected set; }

        public byte[] BlockIds;
        public byte[] BlockMetas;

        /// <summary>
        /// Schematic file constructor
        /// </summary>
        /// <param name="schematicName">Schematic name (without extension)</param>
        public Schematic(string schematicName)
        {
            SchematicName = schematicName;
        }

        public void Reset()
        {
            Height = 0;
            Length = 0;
            Width = 0;
            Level = string.Empty;
            BlockIds = null;
            BlockMetas = null;
        }

        public bool Validate(bool headerOnly = false)
        {
            if (Height == 0 || Length == 0 || Width == 0 || string.IsNullOrEmpty(Level))
                return false;
            if (!headerOnly)
            {
                if (BlockIds == null || BlockMetas == null)
                    return false;
                int blocks = Height*Width*Length;
                if (BlockIds.Length != blocks || BlockMetas.Length != blocks)
                    return false;
            }
            return true;
        }

        public bool LoadFromFile(bool headerOnly = false)
        {
            Reset();
            NBTFile nbtFile = null;
            FileStream stream = null;
            string fileName = Path.Combine(SchematicsPlugin.SchematicsFolder, SchematicName + ".schematic");
            if (!File.Exists(fileName))
                throw new FileNotFoundException("Schematic file not found", SchematicName);

            try
            {
                stream = new FileStream(fileName, FileMode.Open);
                nbtFile = NBTFile.OpenFile(stream, 1);
                foreach (KeyValuePair<string, NBTTag> sa in nbtFile.Contents)
                {
                    switch (sa.Key)
                    {
                        case "Height":
                            Height = sa.Value.Payload;
                            break;
                        case "Length":
                            Length = sa.Value.Payload;
                            break;
                        case "Width":
                            Width = sa.Value.Payload;
                            break;
                        case "Entities":
                        case "TileEntities":
                            break;
                        case "Materials":
                            Level = sa.Value.Payload;
                            break;
                        case "Blocks":
                            if (!headerOnly)
                                BlockIds = sa.Value.Payload;
                            break;
                        case "Data":
                            if (!headerOnly)
                                BlockMetas = sa.Value.Payload;
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Error loading schematic file {0}:", SchematicName), ex);
            }
            finally
            {
                if (stream != null)
                    stream.Dispose();
                if (nbtFile != null)
                    nbtFile.Dispose();
            }

            if (!Validate(headerOnly))
            {
                Reset();
                return false;
            }
            return true;
        }

        public int ToIndex(int x, int y, int z)
        {
            return Width*(y*Length + z) + x;
        }
    }
}
