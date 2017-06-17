// SilverSim is distributed under the terms of the
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
using SilverSim.ServiceInterfaces.Purge;
using SilverSim.Types;
using SilverSim.Types.Primitive;
using System;

namespace SilverSim.Database.MySQL.SimulationData
{
    public sealed partial class MySQLSimulationDataStorage : IAssetReferenceInfoServiceInterface
    {
        public void EnumerateUsedAssets(Action<UUID> action)
        {
            using (MySqlConnection conn = new MySqlConnection(m_ConnectionString))
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
                using (MySqlCommand cmd = new MySqlCommand("SELECT PrimitiveShapeData, ParticleSystem, TextureEntryBytes, ProjectionData, LoopedSoundData, ImpactSoundData FROM prims", conn))
                {
                    using (MySqlDataReader dbReader = cmd.ExecuteReader())
                    {
                        while (dbReader.Read())
                        {
                            ObjectPart.PrimitiveShape shape = new ObjectPart.PrimitiveShape { Serialization = dbReader.GetBytes("PrimitiveShapeData") };
                            ParticleSystem particleSystem = new ParticleSystem(dbReader.GetBytes("ParticleSystem"), 0);
                            TextureEntry te = new TextureEntry(dbReader.GetBytes("TextureEntryBytes"));
                            ObjectPart.ProjectionParam proj = new ObjectPart.ProjectionParam { DbSerialization = dbReader.GetBytes("ProjectionData") };
                            ObjectPart.SoundParam sound = new ObjectPart.SoundParam { Serialization = dbReader.GetBytes("LoopedSoundData") };
                            ObjectPart.CollisionSoundParam colsound = new ObjectPart.CollisionSoundParam { Serialization = dbReader.GetBytes("ImpactSoundData") };
                            if(shape.SculptMap != UUID.Zero)
                            {
                                action(shape.SculptMap);
                            }
                            foreach(UUID refid in particleSystem.References)
                            {
                                action(refid);
                            }
                            foreach (UUID refid in te.References)
                            {
                                action(refid);
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
