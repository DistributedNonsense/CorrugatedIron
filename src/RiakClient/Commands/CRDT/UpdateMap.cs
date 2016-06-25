namespace RiakClient.Commands.CRDT
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Messages;

    /// <summary>
    /// Command used to update a Map in Riak. As a convenience, a builder method
    /// is provided as well as an object with a fluent API for constructing the
    /// update. See <see cref="UpdateMap.MapOperation"/>
    /// <code>
    /// var mapOp = new UpdateMap.MapOperation();
    /// mapOp.IncrementCounter("counter_1", 50)
    ///     .AddToSet("set_1", "set_value_1")
    ///     .SetRegister("register_1", "register_value_1")
    ///     .SetFlag("flag_1", true)
    ///     .Map("inner_map")
    ///         .IncrementCounter("counter_1", 50)
    ///         .AddToSet("set_2", "set_value_2");
    /// </code>
    /// See <see cref="UpdateMap.Builder"/>
    /// <code>
    /// var update = new UpdateMap.Builder(mapOp)
    ///           .WithBucketType("maps")
    ///           .WithBucket("myBucket")
    ///           .WithKey("map_1")
    ///           .WithReturnBody(true)
    ///           .Build();
    /// </code>
    /// </summary>
    public class UpdateMap : UpdateCommand<MapResponse>
    {
        private readonly UpdateMapOptions mapOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateMap"/> class.
        /// </summary>
        /// <param name="options">Options for this operation. See <see cref="UpdateMapOptions"/></param>
        public UpdateMap(UpdateMapOptions options)
            : base(options)
        {
            this.mapOptions = options;
        }

        protected override DtOp GetRequestOp()
        {
            var op = new DtOp();
            op.map_op = Populate(mapOptions.Op);
            return op;
        }

        protected override MapResponse CreateResponse(DtUpdateResp response)
        {
            RiakString key = GetKey(CommandOptions.Key, response);
            return new MapResponse(key, response.context, response.map_value);
        }

        private static MapOp Populate(MapOperation mapOperation)
        {
            var mapOp = new MapOp();

            if (mapOperation.HasRemoves)
            {
                foreach (var removeCounter in mapOperation.RemoveCounters)
                {
                    RiakString counterName = removeCounter.Key;
                    var field = new MapField
                    {
                        name = counterName,
                        type = MapField.MapFieldType.COUNTER
                    };

                    mapOp.removes.Add(field);
                }

                foreach (var removeSet in mapOperation.RemoveSets)
                {
                    RiakString setName = removeSet.Key;
                    var field = new MapField
                    {
                        name = setName,
                        type = MapField.MapFieldType.SET
                    };

                    mapOp.removes.Add(field);
                }

                foreach (var removeRegister in mapOperation.RemoveRegisters)
                {
                    RiakString registerName = removeRegister.Key;
                    var field = new MapField
                    {
                        name = registerName,
                        type = MapField.MapFieldType.REGISTER
                    };

                    mapOp.removes.Add(field);
                }

                foreach (var removeFlag in mapOperation.RemoveFlags)
                {
                    RiakString flagName = removeFlag.Key;
                    var field = new MapField
                    {
                        name = flagName,
                        type = MapField.MapFieldType.FLAG
                    };

                    mapOp.removes.Add(field);
                }

                foreach (var removeMap in mapOperation.RemoveMaps)
                {
                    RiakString mapName = removeMap.Key;
                    var field = new MapField
                    {
                        name = mapName,
                        type = MapField.MapFieldType.MAP
                    };

                    mapOp.removes.Add(field);
                }
            }

            foreach (var incrementCounter in mapOperation.IncrementCounters)
            {
                RiakString counterName = incrementCounter.Key;
                long increment = incrementCounter.Value;

                var field = new MapField
                {
                    name = counterName,
                    type = MapField.MapFieldType.COUNTER
                };

                var counterOp = new CounterOp
                {
                    increment = increment
                };

                var update = new MapUpdate
                {
                    field = field,
                    counter_op = counterOp
                };

                mapOp.updates.Add(update);
            }

            foreach (var addToSet in mapOperation.AddToSets)
            {
                RiakString setName = addToSet.Key;
                IList<RiakString> setAdds = addToSet.Value;

                var field = new MapField
                {
                    name = setName,
                    type = MapField.MapFieldType.SET
                };

                var setOp = new SetOp();
                setOp.adds.AddRange(setAdds.Select(v => (byte[])v));

                var update = new MapUpdate
                {
                    field = field,
                    set_op = setOp
                };

                mapOp.updates.Add(update);
            }

            foreach (var removeFromSet in mapOperation.RemoveFromSets)
            {
                RiakString setName = removeFromSet.Key;
                IList<RiakString> setRemoves = removeFromSet.Value;

                var field = new MapField
                {
                    name = setName,
                    type = MapField.MapFieldType.SET
                };

                var setOp = new SetOp();
                setOp.removes.AddRange(setRemoves.Select(v => (byte[])v));

                var update = new MapUpdate
                {
                    field = field,
                    set_op = setOp
                };

                mapOp.updates.Add(update);
            }

            foreach (var registerToSet in mapOperation.RegistersToSet)
            {
                RiakString registerName = registerToSet.Key;
                RiakString registerValue = registerToSet.Value;

                var field = new MapField
                {
                    name = registerName,
                    type = MapField.MapFieldType.REGISTER
                };

                var update = new MapUpdate
                {
                    field = field,
                    register_op = registerValue
                };

                mapOp.updates.Add(update);
            }

            foreach (var flagToSet in mapOperation.FlagsToSet)
            {
                RiakString flagName = flagToSet.Key;
                bool flagValue = flagToSet.Value;

                var field = new MapField
                {
                    name = flagName,
                    type = MapField.MapFieldType.FLAG
                };

                var update = new MapUpdate
                {
                    field = field,
                    flag_op = flagValue ? MapUpdate.FlagOp.ENABLE : MapUpdate.FlagOp.DISABLE
                };

                mapOp.updates.Add(update);
            }

            foreach (var map in mapOperation.Maps)
            {
                RiakString mapName = map.Key;
                MapOperation innerMapOperation = map.Value;

                var field = new MapField
                {
                    name = mapName,
                    type = MapField.MapFieldType.MAP
                };

                MapOp innerMapOp = Populate(innerMapOperation);

                var update = new MapUpdate
                {
                    field = field,
                    map_op = innerMapOp
                };

                mapOp.updates.Add(update);
            }

            return mapOp;
        }

        public class MapOperation
        {
            private readonly CounterOperations incrementCounters = new CounterOperations();
            private readonly CounterOperations removeCounters = new CounterOperations();

            private readonly SetOperations addToSets = new SetOperations();
            private readonly SetOperations removeFromSets = new SetOperations();
            private readonly SetOperations removeSets = new SetOperations();

            private readonly RegisterOperations registersToSet = new RegisterOperations();
            private readonly RegisterOperations removeRegisters = new RegisterOperations();

            private readonly FlagOperations flagsToSet = new FlagOperations();
            private readonly FlagOperations removeFlags = new FlagOperations();

            private readonly MapOperations maps = new MapOperations();
            private readonly MapOperations removeMaps = new MapOperations();

            public bool HasRemoves
            {
                get
                {
                    bool hasNestedRemoves = GetNestedRemoves(maps);

                    return hasNestedRemoves ||
                        removeCounters.Count > 0 ||
                        removeFromSets.Count > 0 ||
                        removeSets.Count > 0 ||
                        removeRegisters.Count > 0 ||
                        removeFlags.Count > 0 ||
                        removeMaps.Count > 0;
                }
            }

            internal CounterOperations IncrementCounters
            {
                get { return incrementCounters; }
            }

            internal CounterOperations RemoveCounters
            {
                get { return removeCounters; }
            }

            internal SetOperations AddToSets
            {
                get { return addToSets; }
            }

            internal SetOperations RemoveFromSets
            {
                get { return removeFromSets; }
            }

            internal SetOperations RemoveSets
            {
                get { return removeSets; }
            }

            internal RegisterOperations RegistersToSet
            {
                get { return registersToSet; }
            }

            internal RegisterOperations RemoveRegisters
            {
                get { return removeRegisters; }
            }

            internal FlagOperations FlagsToSet
            {
                get { return flagsToSet; }
            }

            internal FlagOperations RemoveFlags
            {
                get { return removeFlags; }
            }

            internal MapOperations Maps
            {
                get { return maps; }
            }

            internal MapOperations RemoveMaps
            {
                get { return removeMaps; }
            }

            public MapOperation IncrementCounter(RiakString key, long increment)
            {
                removeCounters.Remove(key);
                incrementCounters.Increment(key, increment);
                return this;
            }

            public MapOperation RemoveCounter(RiakString key)
            {
                incrementCounters.Remove(key);
                removeCounters.Add(key);
                return this;
            }

            public MapOperation AddToSet(RiakString key, RiakString value)
            {
                removeSets.Remove(key);
                addToSets.Add(key, value);
                return this;
            }

            public MapOperation AddToSet(RiakString key, IEnumerable<string> values)
            {
                return AddToSet(key, values.Select(v => new RiakString(v)));
            }

            public MapOperation AddToSet(RiakString key, IEnumerable<RiakString> values)
            {
                removeSets.Remove(key);

                foreach (var value in values)
                {
                    addToSets.Add(key, value);
                }

                return this;
            }

            public MapOperation RemoveFromSet(RiakString key, RiakString value)
            {
                removeSets.Remove(key);
                removeFromSets.Add(key, value);
                return this;
            }

            public MapOperation RemoveFromSet(RiakString key, IEnumerable<string> values)
            {
                return RemoveFromSet(key, values.Select(v => new RiakString(v)));
            }

            public MapOperation RemoveFromSet(RiakString key, IEnumerable<RiakString> values)
            {
                removeSets.Remove(key);

                foreach (var value in values)
                {
                    removeFromSets.Add(key, value);
                }

                return this;
            }

            public MapOperation RemoveSet(RiakString key)
            {
                addToSets.Remove(key);
                removeFromSets.Remove(key);
                removeSets.Add(key);
                return this;
            }

            public MapOperation SetRegister(RiakString key, RiakString value)
            {
                removeRegisters.Remove(key);
                registersToSet.Add(key, value);
                return this;
            }

            public MapOperation RemoveRegister(RiakString key)
            {
                registersToSet.Remove(key);
                removeRegisters.Add(key);
                return this;
            }

            public MapOperation SetFlag(RiakString key, bool value)
            {
                removeFlags.Remove(key);
                flagsToSet.Add(key, value);
                return this;
            }

            public MapOperation RemoveFlag(RiakString key)
            {
                flagsToSet.Remove(key);
                removeFlags.Add(key);
                return this;
            }

            public MapOperation Map(RiakString key)
            {
                removeMaps.Remove(key);

                MapOperation mapOp;
                if (!maps.TryGetValue(key, out mapOp))
                {
                    mapOp = new MapOperation();
                    maps.Add(key, mapOp);
                }

                return mapOp;
            }

            public MapOperation RemoveMap(RiakString key)
            {
                maps.Remove(key);
                removeMaps.Add(key);
                return this;
            }

            private static bool GetNestedRemoves(MapOperations maps)
            {
                bool hasNestedRemoves = false;

                if (maps != null)
                {
                    foreach (var mapOperation in maps)
                    {
                        hasNestedRemoves =
                            mapOperation.Value.HasRemoves || GetNestedRemoves(mapOperation.Value.Maps);
                        if (hasNestedRemoves)
                        {
                            break;
                        }
                    }
                }

                return hasNestedRemoves;
            }

            internal abstract class MapOperations<TValue> : Dictionary<RiakString, TValue>
            {
                public void Add(RiakString key)
                {
                    if (key == null)
                    {
                        throw new ArgumentNullException("key");
                    }

                    this[key] = default(TValue);
                }
            }

            internal class CounterOperations : MapOperations<long>
            {
                public void Increment(RiakString key, long increment)
                {
                    if (this.ContainsKey(key))
                    {
                        this[key] += increment;
                    }
                    else
                    {
                        this[key] = increment;
                    }
                }
            }

            internal class SetOperations : MapOperations<IList<RiakString>>
            {
                public void Add(RiakString key, RiakString value)
                {
                    IList<RiakString> adds = null;

                    if (!this.TryGetValue(key, out adds))
                    {
                        adds = new List<RiakString>();
                        this[key] = adds;
                    }

                    adds.Add(value);
                }
            }

            internal class RegisterOperations : MapOperations<RiakString>
            {
                public void SetRegister(RiakString key, RiakString value)
                {
                    this[key] = value;
                }
            }

            internal class FlagOperations : MapOperations<bool>
            {
                public void SetFlag(RiakString key, bool value)
                {
                    this[key] = value;
                }
            }

            internal class MapOperations : MapOperations<MapOperation>
            {
            }
        }

        public class Builder :
            UpdateCommandBuilder<Builder, UpdateMap, UpdateMapOptions, MapResponse>
        {
            private MapOperation mapOp;

            public Builder()
            {
            }

            public Builder(MapOperation mapOp)
            {
                InitMapOp(mapOp);
            }

            public Builder(MapOperation mapOp, Builder source)
                : base(source)
            {
                InitMapOp(mapOp);
            }

            public Builder WithMapOperation(MapOperation mapOp)
            {
                this.mapOp = mapOp;
                return this;
            }

            protected override void PopulateOptions(UpdateMapOptions options)
            {
                if (mapOp == null)
                {
                    throw new InvalidOperationException("Missing required MapOperation!");
                }

                options.Op = mapOp;
            }

            private void InitMapOp(MapOperation mapOp)
            {
                if (mapOp == null)
                {
                    throw new ArgumentNullException("mapOp", "mapOp is required.");
                }
                else
                {
                    this.mapOp = mapOp;
                }
            }
        }
    }
}
