using System;
using System.IO;

namespace MiniFatFs
{
    
    public class VirtualDisk
    {
        private FileStream _diskStream;
        private string _diskPath;

        public void Initialize(string path)
        {
            _diskPath = path;
            bool createNew = !File.Exists(path);

            _diskStream = new FileStream(
                path, 
                FileMode.OpenOrCreate, 
                FileAccess.ReadWrite, 
                FileShare.None
            );

            if (createNew)
            {
                Console.WriteLine($"Creating new virtual disk at: {_diskPath}");
                _diskStream.SetLength(FsConstants.DISK_SIZE);
            }
            else
            {
                if (_diskStream.Length != FsConstants.DISK_SIZE)
                {
                    throw new IOException($"Disk file size is incorrect. Expected {FsConstants.DISK_SIZE} bytes, found {_diskStream.Length} bytes.");
                }
                Console.WriteLine($"Opened existing virtual disk at: {_diskPath}");
            }
        } 

        public byte[] ReadCluster(int clusterNumber)
        {
            ValidateCluster(clusterNumber); 

            long offset = (long)clusterNumber * FsConstants.CLUSTER_SIZE; 
            byte[] data = new byte[FsConstants.CLUSTER_SIZE];
            
            _diskStream.Seek(offset, SeekOrigin.Begin);
            int bytesRead = _diskStream.Read(data, 0, FsConstants.CLUSTER_SIZE);

            if (bytesRead != FsConstants.CLUSTER_SIZE)
            {
                // This case handles reading beyond the actual data, which can happen 
                // if the disk was not properly sized.
                // In a 1MB disk, this should only happen if the cluster num is wrong.
                // For simplicity, we assume this is an I/O error as per Task 1.
                throw new IOException($"Failed to read full cluster {clusterNumber}. Read {bytesRead} bytes.");
            }

            return data;
        } 

        public void WriteCluster(int clusterNumber, byte[] data)
        {
            ValidateCluster(clusterNumber); 
            if (data.Length != FsConstants.CLUSTER_SIZE)
            {
                throw new ArgumentException($"Data array must be exactly {FsConstants.CLUSTER_SIZE} bytes long.");
            }

            long offset = (long)clusterNumber * FsConstants.CLUSTER_SIZE; 

            _diskStream.Seek(offset, SeekOrigin.Begin);
            _diskStream.Write(data, 0, FsConstants.CLUSTER_SIZE);
            _diskStream.Flush(); 
        } 

        public long GetDiskSize()
        {
            return FsConstants.DISK_SIZE;
        } 

        public void CloseDisk()
        {
            if (_diskStream != null)
            {
                _diskStream.Close(); 
                _diskStream.Dispose();
                _diskStream = null;
                Console.WriteLine($"Closed virtual disk: {_diskPath}");
            }
        }

        private void ValidateCluster(int clusterNumber)
        {
            if (clusterNumber < 0 || clusterNumber > FsConstants.MAX_CLUSTER_NUM)
            {
                throw new ArgumentOutOfRangeException(nameof(clusterNumber), 
                    $"Cluster number {clusterNumber} is out of bounds (0 to {FsConstants.MAX_CLUSTER_NUM}).");
            }
        }
    }
}