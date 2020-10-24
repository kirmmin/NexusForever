using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NexusForever.Shared;
using NexusForever.Shared.Game;
using NexusForever.Shared.GameTable;
using NexusForever.Shared.GameTable.Model;
using NexusForever.WorldServer.Game.Entity.Movement.Generator;
using NexusForever.WorldServer.Game.Entity.Movement.Spline;
using NexusForever.WorldServer.Game.Entity.Movement.Spline.Static;
using NexusForever.WorldServer.Game.Entity.Network;
using NexusForever.WorldServer.Game.Entity.Network.Command;
using NexusForever.WorldServer.Network.Message.Model;

namespace NexusForever.WorldServer.Game.Entity.Movement
{
    public class MovementManager : IUpdate, IEnumerable<(EntityCommand, IEntityCommandModel)>
    {
        private const double SplineGridUpdateTime = 1d;

        private readonly WorldEntity owner;
        
        private readonly Dictionary<EntityCommand, IEntityCommandModel> commands = new Dictionary<EntityCommand, IEntityCommandModel>();

        private EntityCommand splineCommand;
        private SplinePath splinePath;
        private readonly UpdateTimer splineGridUpdateTimer = new UpdateTimer(SplineGridUpdateTime);
        private UpdateTimer chaseReUseTimer = new UpdateTimer(0.25d, false);

        private bool isDirty;
        private bool serverControlled = true;
        private uint time = 1u;
        private EntityCommand[] handledCommands = new EntityCommand[]
        {
            EntityCommand.SetPosition,
            EntityCommand.SetRotation,
            EntityCommand.SetPlatform
        };

        /// <summary>
        /// Create a new <see cref="MovementManager"/> for supplied <see cref="WorldEntity"/>.
        /// </summary>
        public MovementManager(WorldEntity entity, Vector3 position, Vector3 rotation)
        {
            owner = entity;

            AddCommand(new SetPositionCommand
            {
                Position = new Position(position)
            });

            AddCommand(new SetRotationCommand
            {
                Position = new Position(rotation)
            });

            AddCommand(new SetVelocityDefaultsCommand());
            AddCommand(new SetMoveDefaultsCommand());
            //AddCommand(new SetRotationDefaultsCommand());
        }

        public void Update(double lastTick)
        {
            BroadcastCommands();

            if (chaseReUseTimer.IsTicking)
                chaseReUseTimer.Update(lastTick);

            if (splinePath != null)
            {
                splinePath.Update(lastTick);
                if (splinePath.IsFinialised)
                {
                    StopSpline();
                    return;
                }

                UpdateSplineCommand();

                splineGridUpdateTimer.Update(lastTick);
                if (splineGridUpdateTimer.HasElapsed)
                {
                    // update grid position with the interpolated position on the spline
                    owner.Map.EnqueueRelocate(owner, splinePath.GetPosition());
                    splineGridUpdateTimer.Reset();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void UpdateSplineCommand()
        {
            float splineOffset = (splinePath.Position % splinePath.Length) / splinePath.Length;
            uint timeTotal     = (uint)((splinePath.Length / splinePath.Speed) * 1000f);
            uint timeOffset    = (uint)(timeTotal * splineOffset);

            switch (splineCommand)
            {
                case EntityCommand.SetPositionPath:
                {
                    SetPositionPathCommand command = GetCommand<SetPositionPathCommand>();
                    command.Offset = timeOffset;
                    break;
                }
            }
        }

        /// <summary>
        /// Create a new <see cref="SetPositionCommand"/> with the supplied position <see cref="Vector3"/>.
        /// </summary>
        /// <remarks>
        /// Be aware that this position doesn't always match the grid position (eg: when on a vehicle)
        /// </remarks>
        public void SetPosition(Vector3 position, bool sendImmediately = true)
        {
            StopSpline();
            AddCommand(new SetPositionCommand
            {
                Position = new Position(position)
            }, sendImmediately);
        }

        /// <summary>
        /// Create a new <see cref="SetRotationCommand"/> with the supplied rotation <see cref="Vector3"/>.
        /// </summary>
        /// <remarks>
        /// Be aware that this rotation doesn't always match the entity rotation (eg: when on a vehicle)
        /// </remarks>
        public void SetRotation(Vector3 rotation, bool blend = false, bool sendImmediately = true)
        {
            StopSpline();

            commands.Remove(EntityCommand.SetRotationDefaults);
            commands.Remove(EntityCommand.SetRotationFaceUnit);
            AddCommand(new SetRotationCommand
            {
                Position = new Position(rotation),
                Blend    = blend
            }, sendImmediately);
        }

        /// <summary>
        /// Get the rotation <see cref="Vector3"/> from <see cref="SetRotationCommand"/>.
        /// </summary>
        public Vector3 GetRotation()
        {
            // Entity is moving along a spline, make sure we return the rotation based on current direction between previous and next point
            if (splinePath != null)
            {
                Vector3 position = splinePath.GetPosition();
                return splinePath.GetPreviousPosition().GetRotationTo(position);
            }

            // Entity is using FaceUnit command, return rotation to unit
            if (GetFaceUnit() != 0u)
            {
                WorldEntity targetEntity = owner.Map?.GetEntity<WorldEntity>(GetFaceUnit());
                if (targetEntity != null)
                    return owner.Position.GetRotationTo(targetEntity.Position);
            }

            // Assumed that no other type of rotation is in effect or needs to be calculated, so return rotation from RotationCommand
            SetRotationCommand command = GetCommand<SetRotationCommand>();
            return command?.Position.Vector ?? Vector3.Zero;
        }

        /// <summary>
        /// Get the unit id from <see cref="SetRotationFaceUnitCommand"/>.
        /// </summary>
        public uint GetFaceUnit()
        {
            SetRotationFaceUnitCommand command = GetCommand<SetRotationFaceUnitCommand>();
            return command?.UnitId > 0 ? command.UnitId : 0u;
        }

        /// <summary>
        /// Get the platform unit id from <see cref="SetPlatformCommand"/>.
        /// </summary>
        public uint? GetPlatform()
        {
            SetPlatformCommand command = GetCommand<SetPlatformCommand>();
            return command?.UnitId;
        }

        /// <summary>
        /// Create a new <see cref="SetPlatformCommand"/> with the supplied platform unit id.
        /// </summary>
        public void SetPlatform(uint unitId)
        {
            StopSpline();
            AddCommand(new SetPlatformCommand
            {
                UnitId = unitId
            }, true);
        }

        /// <summary>
        /// Launch a new single spline with supplied <see cref="SplineMode"/> and speed.
        /// </summary>
        public void LaunchSpline(ushort splineId, SplineMode mode, float speed)
        {
            Spline2Entry entry = GameTableManager.Instance.Spline2.GetEntry(splineId);
            if (entry == null)
                throw new ArgumentOutOfRangeException();

            if (speed < float.Epsilon)
                throw new ArgumentOutOfRangeException();

            StopSpline();
            splinePath = new SplinePath(splineId, mode, speed);

            // TODO: This forces the entity to face the direction of travel. Need a way to handle walking backwards.
            AddCommand(new SetRotationDefaultsCommand
            {
                Blend = true
            });

            splineCommand = EntityCommand.SetPositionSpline;
            AddCommand(new SetPositionSplineCommand
            {
                SplineId = splineId,
                Speed    = speed,
                Mode     = mode
            });

            // TODO: retail sent SetStateKeysCommand which sets the state for a limited time
            AddCommand(new SetStateCommand
            {
                State = 258
            }, true);
        }

        /// <summary>
        /// Launch a new multi spline with supplied <see cref="SplineMode"/> and speed.
        /// </summary>
        public void LaunchSpline(List<ushort> splineIds, SplineMode mode, float speed)
        {
            // TODO: implement multi spline, this is used for taxis
            throw new NotImplementedException();
        }

        /// <summary>
        /// Launch a new custom spline with supplied <see cref="SplineType"/>, <see cref="SplineMode"/> and speed.
        /// </summary>
        public void LaunchSpline(List<Vector3> nodes, SplineType type, SplineMode mode, float speed)
        {
            switch (type)
            {
                // linear requires at minimum 2 control points
                case SplineType.Linear when nodes.Count < 2:
                    throw new ArgumentOutOfRangeException();
                // catmullrom requires at minimum 2 amplitude and 2 control points
                case SplineType.CatmullRom when nodes.Count < 4:
                    throw new ArgumentOutOfRangeException();
            }

            if (speed < float.Epsilon)
                throw new ArgumentOutOfRangeException();

            StopSpline();
            splinePath = new SplinePath(nodes, type, mode, speed);

            // TODO: This forces the entity to face the direction of travel. Need a way to handle walking backwards.
            AddCommand(new SetRotationDefaultsCommand
            {
                Blend = true
            });

            splineCommand = EntityCommand.SetPositionPath;
            AddCommand(new SetPositionPathCommand
            {
                Positions = nodes.Select(n => new Position(n)).ToList(),
                Speed     = speed,
                Type      = type,
                Mode      = mode
            });

            // TODO: retail sent SetStateKeysCommand which sets the state for a limited time
            AddCommand(new SetStateCommand
            {
                State = 258
            }, true);
        }

        /// <summary>
        /// Stops the current active spline, relocating the owner to the interpolated position.
        /// </summary>
        public void StopSpline()
        {
            if (splinePath == null)
                return;

            Vector3 position = splinePath.GetPosition();
            owner.Map.EnqueueRelocate(owner, position);

            AddCommand(new SetStateCommand
            {
                State = 0
            });

            AddCommand(new SetRotationFaceUnitCommand
            {
                UnitId = 0
            });

            AddCommand(new SetRotationCommand
            {
                Position = new Position(splinePath.GetPreviousPosition().GetRotationTo(position))
            });

            AddCommand(new SetPositionCommand
            {
                Position = new Position(position),
                Blend = true
            }, true);

            // TODO: calculate spline gradient to set rotation on end

            commands.Remove(splineCommand);
            splinePath = null;
        }

        /// <summary>
        /// Broadcast current commands if changes have occured since the last broadcast.
        /// </summary>
        public void BroadcastCommands()
        {
            if (!isDirty)
                return;

            var serverEntityCommand = new ServerEntityCommand
            {
                Guid             = owner.Guid,
                Time             = time,
                TimeReset        = serverControlled,
                ServerControlled = serverControlled
            };

            foreach ((EntityCommand command, IEntityCommandModel entityCommand) in commands)
                serverEntityCommand.Commands.Add((command, entityCommand));

            owner.EnqueueToVisible(serverEntityCommand, true);
            ClearUnhandledCommands();

            isDirty = false;
            serverControlled = true;
        }

        /// <summary>
        /// Add a new <see cref="IEntityCommandModel"/>, this will replaced the existing command of this type.
        /// </summary>
        private void AddCommand(IEntityCommandModel model, bool dirty = false)
        {
            EntityCommand? command = EntityCommandManager.Instance.GetCommand(model.GetType());
            if (command == null)
                throw new ArgumentException();

            commands.Remove(command.Value);
            commands.Add(command.Value, model);

            if (dirty)
                isDirty = true;
        }

        /// <summary>
        /// Launch a new custom linear spline where the points are generated by <see cref="IMovementGenerator"/>.
        /// </summary>
        public void LaunchGenerator(IMovementGenerator generator, float speed, SplineMode mode = SplineMode.OneShot)
        {
            List<Vector3> nodes = generator.CalculatePath();
            LaunchSpline(nodes, SplineType.Linear, mode, speed);
        }

        public void Chase(WorldEntity entity, float speed, float distance)
        {
            if (chaseReUseTimer.IsTicking && !chaseReUseTimer.HasElapsed)
                return;

            float currentDistance = owner.Position.GetDistance(entity.Position);
            if (currentDistance <= distance)
            {
                if (splinePath == null)
                {
                    //System.Diagnostics.Debug.Assert(GetFaceUnit() == 0);
                    if (GetFaceUnit() != entity.Guid)
                    {
                        commands.Remove(EntityCommand.SetRotation);
                        commands.Remove(EntityCommand.SetRotationDefaults);
                        AddCommand(new SetRotationFaceUnitCommand
                        {
                            UnitId = entity.Guid
                        }, true);
                    }
                    return;
                }

                StopSpline();
                BroadcastCommands();

                commands.Remove(EntityCommand.SetRotation);
                commands.Remove(EntityCommand.SetRotationDefaults);
                AddCommand(new SetRotationFaceUnitCommand
                {
                    UnitId = entity.Guid
                }, true);
                chaseReUseTimer.Reset(true);

                return;
            }

            AddCommand(new SetRotationFaceUnitCommand
            {
                UnitId = entity.Guid,
                Blend = true
            });

            var generator = new DirectMovementGenerator
            {
                Begin = splinePath?.GetPosition() ?? owner.Position,
                Final = entity.Position,
                Map = entity.Map
            };
            
            LaunchGenerator(generator, speed);
        }

        /// <summary>
        /// Move the <see cref="WorldEntity"/> owner to the specified location. 
        /// </summary>
        /// <returns>Returns true when the <see cref="WorldEntity"/> has reached the location.</returns>
        public bool MoveTo(Vector3 location, float speed)
        {
            float currentDistance = location.GetDistance(owner.Position);
            if (currentDistance < 0.2f)
            {
                StopSpline();

                commands.Remove(EntityCommand.SetRotationFaceUnit);
                commands.Remove(EntityCommand.SetRotationDefaults);
                System.Diagnostics.Debug.Assert(GetFaceUnit() == 0);
                BroadcastCommands();
                return true;
            }

            AddCommand(new SetRotationFaceUnitCommand
            {
                UnitId = 0
            });

            AddCommand(new SetRotationDefaultsCommand
            {
                Blend = true
            });

            var generator = new DirectMovementGenerator
            {
                Begin = splinePath?.GetPosition() ?? owner.Position,
                Final = location,
                Map = owner.Map
            };
            LaunchGenerator(generator, speed);

            return false;
        }

        public void Follow(WorldEntity entity, float distance)
        {
            AddCommand(new SetRotationFaceUnitCommand
            {
                UnitId = entity.Guid
            });

            // angle is directly behind entity being followed
            float angle = -entity.Rotation.X;
            angle += MathF.PI / 2;

            var generator = new DirectMovementGenerator
            {
                Begin = splinePath?.GetPosition() ?? owner.Position,
                Final = entity.Position.GetPoint2D(angle, distance),
                Map   = entity.Map
            };

            // TODO: calculate speed based on entity being followed.
            LaunchGenerator(generator, 8f);
        }

        public void HandleClientEntityCommands(List<(EntityCommand, IEntityCommandModel)> commands, uint time)
        {
            foreach ((EntityCommand id, IEntityCommandModel command) in commands)
            {
                switch (command)
                {
                    case SetPositionCommand setPosition:
                        {
                            // TODO: Investigate a better way to check for spell cancellation. Comparing vectors every moment could be expensive.
                            // There is a slight "judder" at the end of a player's movement, but the Vector difference is so small and they should be able to cast, but was getting blocked because the command is still sent.
                            if (owner is Player player && player.IsCasting() && setPosition.Position.Vector.GetDistance(GetCommand<SetPositionCommand>().Position.Vector) > 0.005f)
                                player.CancelSpellsOnMove();

                            owner.Map.EnqueueRelocate(owner, setPosition.Position.Vector);
                            break;
                        }
                    case SetRotationCommand setRotation:
                        owner.Rotation = setRotation.Position.Vector;
                        break;
                }

                AddCommand(command);
            }

            this.time = time;
            serverControlled = false;
            isDirty = true;
            BroadcastCommands();
        }

        private void ClearUnhandledCommands()
        {
            if (serverControlled)
                return;

            foreach (EntityCommand command in commands.Keys.ToList())
            {
                if (handledCommands.Contains(command))
                    continue;

                commands.Remove(command);
            }
        }

        private T GetCommand<T>() where T : IEntityCommandModel
        {
            EntityCommand? command = EntityCommandManager.Instance.GetCommand(typeof(T));
            if (command == null)
                throw new ArgumentException();

            if (!commands.TryGetValue(command.Value, out IEntityCommandModel model))
                return default;
            return (T)model;
        }

        public IEnumerator<(EntityCommand, IEntityCommandModel)> GetEnumerator()
        {
            return commands
                .Select(c => (c.Key, c.Value))
                .GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
