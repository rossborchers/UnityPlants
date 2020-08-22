using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;

public class StreamingGrid : MonoBehaviour
{
        public Transform Target;
        public Camera CullCamera;
        [Space]
        public bool3 Collapse;

        [SerializeField]
        public int3 LoadExtents;
        public float BlockSize;

        private Dictionary<uint, int3> _validChunks;
        private HashSet<uint> _loadingChunks; //chunks that are waiting to load (not yet existing)
        private HashSet<uint> _unloadingChunks; //chunks that are waiting to unload (valid and exist until unload is finished, unloads should be atomic)

        /// <summary>
        /// Please ensure to call the action that signals the load is complete so the internal model can be updated.
        /// Hash, Position, Block Size, Complete Action
        /// </summary>
        public event Action<uint, float3, float, Action> OnChunkLoadBegin = delegate {  };

        /// <summary>
        /// Please ensure to call the action that signals the unload is complete so the internal model can be updated.
        /// Hash, Position, Block Size, Complete Action
        /// </summary>
        public event Action<uint, float3, float, Action> OnChunkUnloadBegin = delegate {  };

        void Start()
        {
            _validChunks = new Dictionary<uint, int3>();
            _loadingChunks = new HashSet<uint>();
            _unloadingChunks = new HashSet<uint>();
        }

        private void Update()
        {
            UpdateChunks();
        }

        void UpdateChunks()
        {
            // Get focal point.
            Vector3 targetPosition = Target.transform.position;
            int3 focalChunk = GridMath.WorldToGrid(targetPosition, BlockSize);

            DebugDrawChunk(GridMath.Clamp(targetPosition, BlockSize), Color.cyan);

            //TODO: fix extents being treated differently (-extents is in bounds. +extents is not) and remove the +0.5 in chunk to world. Figure out a more elegant approach? maybe

            Profiler.BeginSample("LevelGenerator - Queue Create Chunks");

            int3 loadSize = LoadExtents * 2;
            loadSize = new int3(Collapse.x ? 1 :loadSize.x,Collapse.y ? 1 :loadSize.y,Collapse.z ? 1 :loadSize.z);
            float volume = loadSize.x * loadSize.y * loadSize.z;
            float blockExtents = BlockSize / 2f;

            // Create new chunks
            for (int i = 0; i < volume; i++)
            {
                //still in chunk space but moved from 1d to 3d
                int3 positiveChunkPosition = GridMath.IndexToPosition(i, loadSize.x,
                    loadSize.y);

                //subtract extents to center position on focal chunk (otherwise it starts at focalChunk and extends into positive only.
                int3 centeredPosition = positiveChunkPosition - new int3(Collapse.x?0:LoadExtents.x,
                                            Collapse.y?0:LoadExtents.y, Collapse.z?0:LoadExtents.z);

                int3 chunkPosition = focalChunk + centeredPosition;

                //Convert to world position and add extents so the blocks corner is at the position of the chunk (chunks origin is in corner, we want it in center)
                float3 chunkWorldPosition = GridMath.GridToWorld(chunkPosition, BlockSize) +
                                            new float3(Collapse.x?0:blockExtents, Collapse.y?0:blockExtents, Collapse.z?0:blockExtents);


                //Dont want to generate a block outside of the cameras view
                if(!BlockInViewFrustum(chunkWorldPosition))
                {
                    continue;
                }

                uint positionHash = math.hash(chunkPosition);
                bool chunkExists = _validChunks.ContainsKey(positionHash);

                //If we are unloading a chunk in a valid cell we need to cancel it before its too late!
                if (_unloadingChunks.Contains(positionHash))
                {
                    _unloadingChunks.Remove(positionHash);
                }

                //Additions
                if (chunkExists)
                {
                    //Draw existing chunk bounds
                    DebugDrawChunk(chunkWorldPosition, new Color(1, 1, 1, 0.25f));
                }
                else if (_loadingChunks.Contains(positionHash))
                {
                    DebugDrawChunk(chunkWorldPosition, Color.green); //Draw green if busy loading
                }
                else
                {
                    //Load a new chunk
                    _loadingChunks.Add(positionHash);
                    OnChunkLoadBegin(positionHash, chunkPosition, BlockSize, () =>
                    {
                        _validChunks.Add(positionHash, chunkPosition);
                        _loadingChunks.Remove(positionHash);
                    });
                }
            }

            Profiler.EndSample();
            Profiler.BeginSample("LevelGenerator - Queue Remove Chunks");

            //Remove out of bounds chunks. Need to iterate over all (cant check just around valid area) to ensure they are removed even if we teleport
            if (_validChunks.Count > volume)
            {
                List<Tuple<uint, int3>> toRemove = new List<Tuple<uint, int3>>();
                foreach (var chunkKVP in _validChunks)
                {
                    int3 pos = chunkKVP.Value;

                    bool alreadyUnloading = _unloadingChunks.Contains(chunkKVP.Key);

                    float3 worldPos = GridMath.GridToWorld(pos, BlockSize) + new float3(blockExtents);
                    //Dont want to keep a block outside of the bounds or the cameras view
                    if (!GridMath.BlockInBounds(pos, focalChunk, LoadExtents) || !BlockInViewFrustum(worldPos))
                    {
                        //Offset position so its centered
                        DebugDrawChunk(worldPos, Color.red);
                        if (!alreadyUnloading)
                        {
                            toRemove.Add(new Tuple<uint, int3>(chunkKVP.Key, chunkKVP.Value));
                        }
                    }
                }

                foreach (Tuple<uint, int3> data in toRemove)
                {
                    uint hash = data.Item1;
                    int3 chunkPosition = data.Item2;
                    _unloadingChunks.Add(hash);
                    OnChunkUnloadBegin(hash, chunkPosition, BlockSize, () =>
                    {
                        _validChunks.Remove(hash); //only mark invalid after removal as removal is atomic and we want to still treat the chunk as active before its unloaded. //TODO: this true?
                        _unloadingChunks.Remove(hash);
                    });
                }
            }
            Profiler.EndSample();
        }

        private bool BlockInViewFrustum(float3 position)
        {
            if (CullCamera != null)
            {
                return GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(CullCamera),
                    new Bounds(position, new float3(BlockSize)));
            }

            //If no cull camera assume always visible
            return true;
        }

        void DebugDrawChunk(Vector3 position, Color color)
        {
            _chunksToDraw.Enqueue(new ChunkDebugData(position, color));
        }

        private Queue<ChunkDebugData> _chunksToDraw = new Queue<ChunkDebugData>();

        private struct ChunkDebugData
        {
            public ChunkDebugData(Vector3 position, Color color)
            {
                Position = position;
                Color = color;
            }
            public Vector3 Position;
            public Color Color;
        }

        private void OnDrawGizmos()
        {
            while (_chunksToDraw.Count > 0)
            {
                ChunkDebugData chunkData = _chunksToDraw.Dequeue();
                Gizmos.color = chunkData.Color;
                Gizmos.DrawWireCube(chunkData.Position,
                    new Vector3(BlockSize, BlockSize,
                        BlockSize));
            }
            Gizmos.color = Color.white;
        }
}
