using System;
using System.Linq;
using System.Collections.Generic;
using MiniFatFs;

class Program
{
    static void FormatFat(FatTableManager manager)
    {
        int[] freshFat = new int[FsConstants.FAT_ARRAY_SIZE];
        
        // 1. Mark all as free initially
        for (int i = 0; i < FsConstants.FAT_ARRAY_SIZE; i++)
        {
            freshFat[i] = FsConstants.FAT_ENTRY_FREE;
        }
        
        // 2. Mark reserved clusters as EOF
        freshFat[FsConstants.SUPERBLOCK_CLUSTER] = FsConstants.FAT_ENTRY_EOF; // Cluster 0
        for (int i = FsConstants.FAT_START_CLUSTER; i <= FsConstants.FAT_END_CLUSTER; i++)
        {
            freshFat[i] = FsConstants.FAT_ENTRY_EOF; // Clusters 1-4
        }
        
        // 3. Mark Root Directory Cluster (5) as allocated (EOF)
        freshFat[FsConstants.ROOT_DIR_FIRST_CLUSTER] = FsConstants.FAT_ENTRY_EOF;

        manager.WriteAllFat(freshFat);
        manager.FlushFatToDisk();
        Console.WriteLine("\nFAT formatted (Clusters 0-5 marked as EOF, others free).");
    }

    static void Main(string[] args)
    {
        string diskPath = "fatty.bin";
        var disk = new VirtualDisk();
        
        try
        {
            disk.Initialize(diskPath);
            
            var fatManager = new FatTableManager(disk);
            FormatFat(fatManager); // Reset the disk and FAT

            var directoryManager = new DirectoryManager(disk, fatManager);
            
            // --- Test 4.1: Add, Find, and List a File Entry ---
            Console.WriteLine("\n--- Test 4.1: Standard Entry Management ---");
            
            // 1. Allocate a cluster chain for the new file (1 cluster)
            int fileStartCluster = fatManager.AllocateChain(1); 
            int fileSize = FsConstants.CLUSTER_SIZE; 
            
            // 2. Define the new entry
            var newEntry = new DirectoryEntry("TestFile.txt", 0x20, fileStartCluster, fileSize);
            
            // 3. Add the entry to the Root Directory (Cluster 5)
            Console.WriteLine($"\nAdding entry: {newEntry.Name} (Cluster: {fileStartCluster}, Size: {fileSize})");
            directoryManager.AddDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, newEntry);
            
            // 4. Verify the entry exists using FindDirectoryEntry (Case-insensitive check)
            string searchName = "testFILE.TXT"; 
            var foundEntry = directoryManager.FindDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, searchName);

            if (foundEntry != null && foundEntry.FirstCluster == fileStartCluster)
            {
                Console.WriteLine($"\tFindDirectoryEntry SUCCESS: Found {foundEntry.Name} (Case-Insensitive check passed).");
            }
            else
            {
                Console.WriteLine("\tFindDirectoryEntry FAILED: Entry not found or data is incorrect.");
                goto EndTests; 
            }
            
            // 5. Verify the entry exists using ReadDirectory (listing)
            var rootEntries = directoryManager.ReadDirectory(FsConstants.ROOT_DIR_FIRST_CLUSTER);
            bool listed = rootEntries.Any(e => e.FirstCluster == fileStartCluster);

            Console.WriteLine(listed ? "\tReadDirectory/Listing SUCCESS: Entry is visible." : "\tReadDirectory/Listing FAILED: Entry not listed.");


            // --- Test 4.2: Remove Entry and Free Cluster Chain ---
            Console.WriteLine("\n--- Test 4.2: Removal and Cluster Freeing Check ---");
            
            // 1. Remove the entry
            Console.WriteLine($"\tAttempting to remove entry {foundEntry.Name}.");
            directoryManager.RemoveDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, foundEntry); 

            // 2. Check the FAT for the file's cluster
            int fatValue = fatManager.GetFatEntry(fileStartCluster);
            bool clusterFreed = fatValue == FsConstants.FAT_ENTRY_FREE;

            Console.WriteLine($"\tFile's cluster ({fileStartCluster}) FAT value after removal: {fatValue}");
            Console.WriteLine(clusterFreed ? "\tFreeChain SUCCESS: Cluster correctly marked FREE." : "\tFreeChain FAILED: Cluster not freed.");

            // 3. Verify the entry is no longer found (slot is empty/ignored)
            var removedEntryCheck = directoryManager.FindDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, searchName);
            Console.WriteLine(removedEntryCheck == null 
                ? "\tRemoval Verification SUCCESS: Entry slot marked empty (0x00)." 
                : "\tRemoval Verification FAILED: Entry still found.");


            // --- Test 4.3: Directory Cluster Extension (Edge Case) ---
            Console.WriteLine("\n--- Test 4.3: Directory Cluster Extension Test ---");
            
            // The directory must grow when more than FsConstants.MAX_ENTRIES_PER_CLUSTER (32) are added.
            int entriesToFill = FsConstants.MAX_ENTRIES_PER_CLUSTER; // 32
            
            Console.WriteLine($"\tFilling Cluster {FsConstants.ROOT_DIR_FIRST_CLUSTER} with {entriesToFill} entries...");
            
            // Allocate a new cluster (which will be the start of the next chain if allocation is successful)
            int firstClusterOfNewChain = 0;
            try
            {
                // Note: We need a reliable way to get the *next* free cluster
                // Since the first free cluster was '6', the next should be '7'.
                firstClusterOfNewChain = fatManager.AllocateChain(1); 
                fatManager.FreeChain(firstClusterOfNewChain); // Free it to use later
            }
            catch (Exception) {} // Ignore potential 'not enough free clusters'

            // Add the 33rd entry
            int finalFileCluster = fatManager.AllocateChain(1); 
            var finalEntry = new DirectoryEntry("LASTFILE.DOC", 0x20, finalFileCluster, 100);
            
            // Loop 33 times. The 33rd entry should trigger the allocation logic.
            for (int i = 0; i <= entriesToFill; i++) 
            {
                if (i == entriesToFill)
                {
                    Console.WriteLine($"\tAdding 33rd entry: {finalEntry.Name} (Should trigger new cluster allocation).");
                    directoryManager.AddDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, finalEntry);
                }
                else
                {
                    // For simplicity, we use the same free cluster slot found in Test 4.2
                    var dummyEntry = new DirectoryEntry($"FILE{i:D2}.DAT", 0x20, FsConstants.CONTENT_START_CLUSTER + 1, 100);
                    directoryManager.AddDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, dummyEntry);
                }
            }

            int fat5 = fatManager.GetFatEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER);

            if (fat5 != FsConstants.FAT_ENTRY_EOF && fat5 >= FsConstants.CONTENT_START_CLUSTER)
            {
                Console.WriteLine($"\tDirectory Extension Check SUCCESS: Cluster 5 linked to Cluster {fat5} (New directory cluster allocated).");
                
                // Final check: find the last entry in the new cluster
                var lastEntryCheck = directoryManager.FindDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, "LASTFILE.DOC");
                Console.WriteLine(lastEntryCheck != null 
                    ? "\tRead/Find in Extended Cluster SUCCESS: Entry is visible." 
                    : "\tRead/Find in Extended Cluster FAILED: Entry not found in new cluster.");
                
                // Cleanup
                directoryManager.RemoveDirectoryEntry(FsConstants.ROOT_DIR_FIRST_CLUSTER, lastEntryCheck);
            }
            else
            {
                Console.WriteLine("\tDirectory Extension Check FAILED: Cluster 5 was NOT correctly linked to a new cluster.");
            }

        EndTests:
            Console.WriteLine("\nTask 4 Test Execution Finished.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nAn error occurred: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            disk.CloseDisk();
        }
    }
}