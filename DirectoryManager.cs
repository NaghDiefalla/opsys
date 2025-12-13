using System;
using System.Collections.Generic;

namespace MiniFatFs
{
    public class DirectoryManager
    {
        private readonly VirtualDisk _disk;
        private readonly FatTableManager _fat;

        private const int ENTRY_SIZE = 32; 

        public DirectoryManager(VirtualDisk disk, FatTableManager fatManager)
        {
            _disk = disk ?? throw new ArgumentNullException(nameof(disk));
            _fat = fatManager ?? throw new ArgumentNullException(nameof(fatManager));
        }

        public List<DirectoryEntry> ReadDirectory(int startCluster)
        {
            List<DirectoryEntry> entries = new List<DirectoryEntry>();
            List<int> clusters = _fat.FollowChain(startCluster);

            foreach (var clusterNum in clusters)
            {
                byte[] cluster = _disk.ReadCluster(clusterNum);
                for (int i = 0; i < cluster.Length; i += ENTRY_SIZE)
                {
                    byte firstByte = cluster[i];
                    if (firstByte == 0x00) continue; // Skip empty entry

                    byte[] nameBytes = new byte[11];
                    Array.Copy(cluster, i, nameBytes, 0, 11);
                    // Name is the 8.3 formatted string from bytes
                    string rawName = Converter.BytesToString(nameBytes, 11); 
                    
                    byte attr = cluster[i + 11];
                    int firstCluster = BitConverter.ToInt32(cluster, i + 20);
                    int fileSize = BitConverter.ToInt32(cluster, i + 28);

                    entries.Add(new DirectoryEntry(DirectoryEntry.Parse8Dot3Name(rawName), attr, firstCluster, fileSize));
                }
            }

            return entries;
        }

        public DirectoryEntry FindDirectoryEntry(int startCluster, string name)
        {
            string formattedName = DirectoryEntry.FormatNameTo8Dot3(name);
            List<int> clusters = _fat.FollowChain(startCluster);

            foreach (var clusterNum in clusters)
            {
                byte[] cluster = _disk.ReadCluster(clusterNum);
                for (int i = 0; i < cluster.Length; i += ENTRY_SIZE)
                {
                    byte firstByte = cluster[i];
                    if (firstByte == 0x00) continue;

                    byte[] nameBytes = new byte[11];
                    Array.Copy(cluster, i, nameBytes, 0, 11);
                    string entryName = Converter.BytesToString(nameBytes, 11);
                    
                    // Case-insensitive comparison is done here:
                    if (entryName.Equals(formattedName, StringComparison.OrdinalIgnoreCase))
                    {
                        byte attr = cluster[i + 11];
                        int firstCluster = BitConverter.ToInt32(cluster, i + 20);
                        int fileSize = BitConverter.ToInt32(cluster, i + 28);
                        
                        // Return the entry using the parsed, readable name
                        return new DirectoryEntry(DirectoryEntry.Parse8Dot3Name(entryName), attr, firstCluster, fileSize);
                    }
                }
            }
            return null;
        }

        public void AddDirectoryEntry(int startCluster, DirectoryEntry newEntry)
        {
            List<int> clusters = _fat.FollowChain(startCluster);

            // 1. Search for a free slot
            foreach (var clusterNum in clusters)
            {
                byte[] cluster = _disk.ReadCluster(clusterNum);

                for (int i = 0; i < cluster.Length; i += ENTRY_SIZE)
                {
                    if (cluster[i] == 0x00)
                    {
                        // Found a free slot, write entry and return
                        WriteEntryToCluster(cluster, i, newEntry);
                        _disk.WriteCluster(clusterNum, cluster);
                        return;
                    }
                }
            }

            // 2. If no free slot found, allocate a new cluster
            int newCluster = _fat.AllocateChain(1);
            
            // Prepare the new cluster data with the entry at the start
            byte[] newClusterData = new byte[FsConstants.CLUSTER_SIZE];
            WriteEntryToCluster(newClusterData, 0, newEntry);
            _disk.WriteCluster(newCluster, newClusterData);
            
            // 3. Link the new cluster to the end of the existing chain
            if (clusters.Count > 0)
            {
                int lastCluster = clusters[clusters.Count - 1];
                _fat.SetFatEntry(lastCluster, newCluster);
            }
            // else: If clusters is empty, this means the 'startCluster' was 0 (FREE), 
            // which shouldn't happen for a directory.

            _fat.FlushFatToDisk();
        }

        public void RemoveDirectoryEntry(int startCluster, DirectoryEntry entry)
        {
            string formattedName = entry.Name;
            List<int> clusters = _fat.FollowChain(startCluster);

            foreach (var clusterNum in clusters)
            {
                byte[] cluster = _disk.ReadCluster(clusterNum);

                for (int i = 0; i < cluster.Length; i += ENTRY_SIZE)
                {
                    byte firstByte = cluster[i];
                    if (firstByte == 0x00) continue;

                    byte[] nameBytes = new byte[11];
                    Array.Copy(cluster, i, nameBytes, 0, 11);
                    string entryName = Converter.BytesToString(nameBytes, 11);
                    
                    if (entryName.Equals(formattedName, StringComparison.OrdinalIgnoreCase))
                    {
                        // 1. Mark the slot as empty (0x00)
                        cluster[i] = 0x00;
                        _disk.WriteCluster(clusterNum, cluster);
                        
                        // 2. Crucially, free the file's data clusters
                        if (entry.FirstCluster >= FsConstants.CONTENT_START_CLUSTER)
                        {
                             _fat.FreeChain(entry.FirstCluster);
                        }
                        
                        _fat.FlushFatToDisk();
                        return;
                    }
                }
            }
        }

        private void WriteEntryToCluster(byte[] cluster, int offset, DirectoryEntry entry)
        {
            // The directory entry holds the 8.3 formatted name (uppercase, padded)
            byte[] nameBytes = Converter.StringToBytes(entry.Name);
            
            Array.Copy(nameBytes, 0, cluster, offset, 11);
            cluster[offset + 11] = entry.Attribute;
            // Offsets based on standard FAT format (Skipping reserved/unused fields)
            Array.Copy(BitConverter.GetBytes(entry.FirstCluster), 0, cluster, offset + 20, 4);
            Array.Copy(BitConverter.GetBytes(entry.FileSize), 0, cluster, offset + 28, 4);
        }
    }
}