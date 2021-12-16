using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Model;
using NexusForever.WorldServer.Game.Entity.Static;
using NexusForever.WorldServer.Game.Map;
using System;
using System.Numerics;

namespace NexusForever.WorldServer.Game.Entity
{
    public class Plug : WorldEntity
    {
        public HousingPlotInfoEntry PlotEntry { get; }
        public HousingPlugItemEntry PlugEntry { get; }

        private Plug ReplacementPlug;
        private Action onAddToMapAction;

        public Plug(HousingPlotInfoEntry plotEntry, HousingPlugItemEntry plugEntry, Action action = null)
            : base(EntityType.Plug)
        {
            PlotEntry = plotEntry;
            PlugEntry = plugEntry;
            onAddToMapAction = action;
            CreatureId = PlugEntry.WorldIdPlug02;
            CreateFlags = EntityCreateFlag.SpawnAnimation;
            DisplayInfo = 22896;
            Properties.Add(Property.BaseHealth, new PropertyValue(Property.BaseHealth, 101f, 101f));
        }

        protected override IEntityModel BuildEntityModel()
        {
            return new PlugModel
            {
                SocketId  = (ushort)(PlotEntry?.WorldSocketId ?? 0u),
                PlugId    = (ushort)(PlugEntry?.WorldIdPlug02 ?? 0u),
                PlugFlags = 63
            };
        }

        /// <summary>
        /// Queue a replacement <see cref="Plug"/> to assume this entity's WorldSocket and WorldPlug location
        /// </summary>
        public void EnqueueReplace(Plug newPlug)
        {
            ReplacementPlug = newPlug;
            RemoveFromMap();
        }

        public override void OnAddToMap(BaseMap map, uint guid, Vector3 vector)
        {
            base.OnAddToMap(map, guid, vector);

            if (onAddToMapAction != null)
            {
                onAddToMapAction.Invoke();
                onAddToMapAction = null;
            }
        }

        public override void OnRemoveFromMap()
        {
            if (ReplacementPlug != null)
            {
                Map?.EnqueueAdd(ReplacementPlug, new MapPosition
                {
                    Position = Position
                });
                ReplacementPlug = null;
            }

            base.OnRemoveFromMap();
        }
    }
}
