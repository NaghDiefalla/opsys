using System;
using System.Collections.Generic;
using System.IO;

namespace MiniFatFs
{
    public class FatTableManager
    {
        private readonly VirtualDisk _disk;
        private int[] _fat = new int[FsConstants.FAT_ARRAY_SIZE]; 

        public FatTableManager(VirtualDisk disk)
        {
            _disk = disk ?? throw new ArgumentNullException(nameof(disk));
            LoadFatFromDisk();
        }

        public void LoadFatFromDisk()
        {
            Console.WriteLine("Loading FAT table from disk clusters 1-4...");
            int fatIndex = 0;
            
            for (int cluster = FsConstants.FAT_START_CLUSTER; cluster <= FsConstants.FAT_END_CLUSTER; cluster++)
            {
                byte[] clusterData = _disk.ReadCluster(cluster);

                for (int i = 0; i < FsConstants.CLUSTER_SIZE; i += FsConstants.FAT_ENTRY_SIZE)
                {
                    if (fatIndex < FsConstants.FAT_ARRAY_SIZE)
                    {
                        _fat[fatIndex] = BitConverter.ToInt32(clusterData, i);
                        fatIndex++;
                    }
                    else
                    {
                        break; 
                    }
                }
            }
            Console.WriteLine("FAT table loaded successfully.");
        }

        public void FlushFatToDisk()
        {
            Console.WriteLine("Flushing FAT table to disk clusters 1-4...");
            int fatIndex = 0;

            for (int cluster = FsConstants.FAT_START_CLUSTER; cluster <= FsConstants.FAT_END_CLUSTER; cluster++)
            {
                byte[] clusterData = new byte[FsConstants.CLUSTER_SIZE];
                
                for (int i = 0; i < FsConstants.CLUSTER_SIZE; i += FsConstants.FAT_ENTRY_SIZE)
                {
                    if (fatIndex < FsConstants.FAT_ARRAY_SIZE)
                    {
                        BitConverter.GetBytes(_fat[fatIndex]).CopyTo(clusterData, i);
                        fatIndex++;
                    }
                    else
                    {
                        break;
                    }
                }
                
                _disk.WriteCluster(cluster, clusterData);
            }
        }

        public int GetFatEntry(int index)
        {
            if (index < 0 || index >= _fat.Length)
                throw new ArgumentOutOfRangeException(nameof(index), "FAT index is out of bounds.");
            
            return _fat[index];
        }

        public void SetFatEntry(int index, int value)
        {
            // Note: This check correctly allows writing to cluster 5 (ROOT_DIR_FIRST_CLUSTER) and above
            if (index < FsConstants.CONTENT_START_CLUSTER)
                throw new ArgumentException($"Cannot write to reserved cluster index {index} (Superblock or FAT region)."); 
            if (index >= _fat.Length)
                throw new ArgumentOutOfRangeException(nameof(index), "FAT index is out of bounds.");

            _fat[index] = value;
        }

        public void WriteAllFat(int[] entries)
        {
            if (entries.Length != FsConstants.FAT_ARRAY_SIZE)
                throw new ArgumentException($"Input array must be exactly {FsConstants.FAT_ARRAY_SIZE} entries long.");
            _fat = entries;
        }

        public List<int> FollowChain(int startCluster)
        {
            if (startCluster < FsConstants.CONTENT_START_CLUSTER || startCluster >= FsConstants.CLUSTER_COUNT)
            {
                return new List<int>();
            }

            List<int> chain = new List<int>();
            int currentCluster = startCluster;

            while (currentCluster != FsConstants.FAT_ENTRY_EOF && currentCluster != FsConstants.FAT_ENTRY_FREE)
            {
                if (currentCluster < FsConstants.CONTENT_START_CLUSTER || currentCluster >= FsConstants.CLUSTER_COUNT)
                {
                    throw new IOException($"FAT chain corruption detected: invalid cluster {currentCluster}.");
                }

                if (chain.Contains(currentCluster))
                {
                     throw new IOException($"FAT chain loop detected at cluster {currentCluster}.");
                }

                chain.Add(currentCluster);
                int nextCluster = GetFatEntry(currentCluster);
                
                currentCluster = nextCluster;
            }

            return chain;
        }

        public int AllocateChain(int requiredClusters)
        {
            if (requiredClusters <= 0) return FsConstants.FAT_ENTRY_EOF;

            List<int> freeClusters = new List<int>();
            
            // Start searching from CONTENT_START_CLUSTER (Cluster 5)
            for (int i = FsConstants.CONTENT_START_CLUSTER; i < FsConstants.CLUSTER_COUNT; i++)
            {
                if (GetFatEntry(i) == FsConstants.FAT_ENTRY_FREE)
                {
                    freeClusters.Add(i);
                    if (freeClusters.Count == requiredClusters) break;
                }
            }

            if (freeClusters.Count < requiredClusters)
            {
                throw new IOException($"Not enough free clusters. Required: {requiredClusters}, Found: {freeClusters.Count}.");
            }
            
            for (int i = 0; i < freeClusters.Count; i++)
            {
                int current = freeClusters[i];
                if (i < freeClusters.Count - 1)
                {
                    _fat[current] = freeClusters[i + 1]; 
                }
                else
                {
                    _fat[current] = FsConstants.FAT_ENTRY_EOF; 
                }
            }

            return freeClusters[0]; 
        }

        public void FreeChain(int startCluster)
        {
            if (startCluster < FsConstants.CONTENT_START_CLUSTER)
            {
                throw new ArgumentException($"Cannot free reserved clusters starting at {startCluster}."); 
            }

            List<int> chainToFree = FollowChain(startCluster);

            foreach (int cluster in chainToFree)
            {
                _fat[cluster] = FsConstants.FAT_ENTRY_FREE; 
            }
        }
    }
}