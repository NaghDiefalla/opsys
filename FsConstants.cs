namespace MiniFatFs
{
    public static class FsConstants 
    {
        public const int CLUSTER_SIZE = 1024;
        public const int CLUSTER_COUNT = 1024;
        public const int DISK_SIZE = CLUSTER_COUNT * CLUSTER_SIZE;

        public const int SUPERBLOCK_CLUSTER = 0; 

        public const int FAT_START_CLUSTER = 1;
        public const int FAT_END_CLUSTER = 4;
        public const int FAT_CLUSTER_COUNT = FAT_END_CLUSTER - FAT_START_CLUSTER + 1;

        public const int FAT_ENTRY_SIZE = 4;
        public const int FAT_ENTRIES_PER_CLUSTER = CLUSTER_SIZE / FAT_ENTRY_SIZE;
        public const int FAT_ARRAY_SIZE = FAT_ENTRIES_PER_CLUSTER * FAT_CLUSTER_COUNT;
        public const int FAT_ENTRY_FREE = 0;
        public const int FAT_ENTRY_EOF = -1; 
        
        public const int CONTENT_START_CLUSTER = 5;
        public const int ROOT_DIR_FIRST_CLUSTER = 5; 

        public const int MAX_CLUSTER_NUM = CLUSTER_COUNT - 1; 

        // Directory Constants derived from your implementation
        public const int DIRECTORY_ENTRY_SIZE = 32;
        public const int MAX_ENTRIES_PER_CLUSTER = CLUSTER_SIZE / DIRECTORY_ENTRY_SIZE; // 32 entries
    }
}