using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using NexusForever.WorldServer.Script;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public class Plug : WorldEntity
    {
        public HousingPlotInfoEntry PlotEntry { get; }
        public HousingPlugItemEntry PlugEntry { get; }

        public Plug(HousingPlotInfoEntry plotEntry, HousingPlugItemEntry plugEntry)
            : base(EntityType.Plug)
        {
            PlotEntry = plotEntry;
            PlugEntry = plugEntry;

            ScriptManager.Instance.GetScript<PlugScript>(PlugEntry.Id)?.OnCreate(this);
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new PlugModel
            {
                SocketId  = (ushort)PlotEntry.WorldSocketId,
                PlugId    = (ushort)PlugEntry.WorldIdPlug00,
                PlugFlags = 63
            };
        }

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            base.OnAddToMap(map, guid, vector);

            ScriptManager.Instance.GetScript<PlugScript>(PlugEntry.Id)?.OnAddToMap(this);
        }
    }
}
