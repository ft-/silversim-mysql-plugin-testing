﻿// SilverSim is distributed under the terms of the
// GNU Affero General Public License v3 with
// the following clarification and special exception.

// Linking this library statically or dynamically with other modules is
// making a combined work based on this library. Thus, the terms and
// conditions of the GNU Affero General Public License cover the whole
// combination.

// As a special exception, the copyright holders of this library give you
// permission to link this library with independent modules to produce an
// executable, regardless of the license terms of these independent
// modules, and to copy and distribute the resulting executable under
// terms of your choice, provided that you also meet, for each linked
// independent module, the terms and conditions of the license of that
// module. An independent module is a module which is not derived from
// or based on this library. If you modify this library, you may extend
// this exception to your version of the library, but you are not
// obligated to do so. If you do not wish to do so, delete this
// exception statement from your version.

using MySql.Data.MySqlClient;
using SilverSim.Scene.Types.Object;
using SilverSim.Scene.Types.Object.Parameters;
using SilverSim.ServiceInterfaces.Purge;
using SilverSim.Types;
using SilverSim.Types.Primitive;
using System;
using System.IO;
using WindLightSettings = SilverSim.Scene.Types.WindLight.EnvironmentSettings;

namespace SilverSim.Database.MySQL.SimulationData
{
    public sealed partial class MySQLSimulationDataStorage : IAssetReferenceInfoServiceInterface
    {
        public void EnumerateUsedAssets(Action<UUID> action)
        {
            using (var conn = new MySqlConnection(m_ConnectionString))
            {
                conn.Open();
                using (MySqlCommand cmd = new MySqlCommand("SELECT DISTINCT AssetId FROM primitems", conn))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            action(dbReader.GetUUID("AssetId"));
                        }
                    }
                }

                using (var cmd = new MySqlCommand("SELECT EnvironmentSettings FROM environmentsettings", conn))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            WindLightSettings settings;
                            using (MemoryStream ms = new MemoryStream(dbReader.GetBytes("EnvironmentSettings")))
                            {
                                settings = WindLightSettings.Deserialize(ms);
                            }
                            if(settings.WaterSettings.NormalMap != UUID.Zero)
                            {
                                action(settings.WaterSettings.NormalMap);
                            }
                        }
                    }
                }

                using (var cmd = new MySqlCommand("SELECT DISTINCT NormalMapTexture FROM lightshare", conn))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id = dbReader.GetUUID("NormalMapTexture");
                            if (id != UUID.Zero)
                            {
                                action(id);
                            }
                        }
                    }
                }

                using (var cmd = new MySqlCommand("SELECT DISTINCT TerrainTexture1, TerrainTexture2, TerrainTexture3, TerrainTexture4 FROM regionsettings", conn))
                { 
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            UUID id;
                            
                            id = dbReader.GetUUID("TerrainTexture1");
                            if (id != UUID.Zero)
                            {
                                action(id);
                            }
                            id = dbReader.GetUUID("TerrainTexture2");
                            if (id != UUID.Zero)
                            {
                                action(id);
                            }
                            id = dbReader.GetUUID("TerrainTexture3");
                            if (id != UUID.Zero)
                            {
                                action(id);
                            }
                            id = dbReader.GetUUID("TerrainTexture4");
                            if (id != UUID.Zero)
                            {
                                action(id);
                            }
                        }
                    }
                }

                using (var cmd = new MySqlCommand("SELECT PrimitiveShapeData, ParticleSystem, TextureEntryBytes, ProjectionData, LoopedSoundData, ImpactSoundData FROM prims", conn))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            var shape = new ObjectPart.PrimitiveShape { Serialization = dbReader.GetBytes("PrimitiveShapeData") };
                            var particleSystem = new ParticleSystem(dbReader.GetBytes("ParticleSystem"), 0);
                            var te = new TextureEntry(dbReader.GetBytes("TextureEntryBytes"));
                            var proj = new ProjectionParam { DbSerialization = dbReader.GetBytes("ProjectionData") };
                            var sound = new SoundParam { Serialization = dbReader.GetBytes("LoopedSoundData") };
                            var colsound = new CollisionSoundParam { Serialization = dbReader.GetBytes("ImpactSoundData") };
                            if(shape.SculptMap != UUID.Zero)
                            {
                                action(shape.SculptMap);
                            }
                            foreach(UUID refid in particleSystem.References)
                            {
                                if (refid != UUID.Zero)
                                {
                                    action(refid);
                                }
                            }
                            foreach (UUID refid in te.References)
                            {
                                if (refid != UUID.Zero)
                                {
                                    action(refid);
                                }
                            }
                            if(proj.ProjectionTextureID != UUID.Zero)
                            {
                                action(proj.ProjectionTextureID);
                            }
                            if(sound.SoundID != UUID.Zero)
                            {
                                action(sound.SoundID);
                            }
                            if(colsound.ImpactSound != UUID.Zero)
                            {
                                action(colsound.ImpactSound);
                            }
                        }
                    }
                }
            }
        }
    }
}
