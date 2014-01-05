using System;
using System.Collections.Generic;
using System.Linq;
//******************************************************
// File: FixedPartitionMemoryManager.cs
//
// Purpose: Contains the class definition for FixedPartitionMemoryManager, an
//          implementation of the IFixedPartitionMemoryManager interface.
//          Manages memory allocation and deallocation using a fixed partition
//          memory management scheme.
//
// Written By: Peter DiSalvo 
//******************************************************

using System.Text;
using SimpleOSInterfaces;
using System.Diagnostics;

namespace FixedPartitionMemoryManager
{
    public class MyFixedPartitionMemoryManager : IDllInfo, IFixedPartitionMemoryManager
    {
        #region Private Variables

        private IOperatingSystemFactory operatingSystemFactory;
        private IOperatingSystemSettings operatingSystemSettings;
        private OperatingSystemMemoryTypes memoryType;
        private ICPU cpu;
        private IProcessorManager processorManager;
        private int userMemoryStartingAddress; // The absolute address where user memory begins.
        private int userMemoryPartitionsOffset; // The number of partitions from the start of memory to the first user partition.
        private Partition[] partitions; // The actual array of partitions that store memory.
        private Partition[] userPartitionPointers; // Array pointing to the partitions that are user partitions.
        private Partition[] osPartitionPointers; // Array pointing to the partitions that are operating system reserved partitions.
        private int partitionCounter; // A counter that is increased each time a partition is added, which aids in determining the index of the next partition.

        #endregion

        //****************************************************
        // Method: MyFixedPartitionMemoryManager
        //
        // Purpose: Memory manager constructor.
        //****************************************************
        public MyFixedPartitionMemoryManager(IOperatingSystemFactory operatingSystemFactory)
        {
            this.operatingSystemFactory = operatingSystemFactory;
            this.memoryType = OperatingSystemMemoryTypes.FIXEDPARTITION;
            this.cpu = (ICPU)this.operatingSystemFactory.CreateObject(ObjectType.ICPU);
            this.processorManager = (IProcessorManager)this.operatingSystemFactory.CreateObject(ObjectType.IProcessorManager);
        }

        #region IDllInfo Methods

        public string GetAuthor()
        {
            return "Peter DiSalvo";
        }

        public string GetCompiler()
        {
            return "VS 2010 C#";
        }

        public string GetDescription()
        {
            return "Fixed partition memory manager implementation";
        }

        #endregion

        #region IFixedPartitionMemoryManager Methods

        //****************************************************
        // Method: AddPartition
        //
        // Purpose: Creates a new partition that encapsulates
        //          a block of memory.
        //****************************************************
        public int AddPartition(int size)
        {
            ++this.partitionCounter;
            IMemoryBlock memBlock = (IMemoryBlock)this.operatingSystemFactory.CreateObject(ObjectType.IMemoryBlock);
            memBlock.SetSize(size);

            int partitionAbsoluteStartingAddress = MyFixedPartitionMemoryManager.calculatePartitionAbsoluteStartingAddress(this.partitionCounter, size,
                                                                                                                this.userMemoryStartingAddress, this.userMemoryPartitionsOffset);

            try
            {
                this.partitions[partitionCounter] = new Partition(this.partitionCounter, partitionAbsoluteStartingAddress, memBlock);
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }

            return this.partitionCounter;
        }

        //****************************************************
        // Method: FreePartitions
        //
        // Purpose: Returns the number of free partitions.
        //****************************************************
        public int FreePartitions()
        {
            int numberFree = 0;
            
            foreach (Partition partition in this.partitions)
            {
                if (!partition.IsUsed)
                {
                    ++numberFree;
                }
            }

            return numberFree;
        }

        //****************************************************
        // Method: GetNumPartitions
        //
        // Purpose: Returns the total number of partitions.
        //****************************************************
        public int GetNumPartitions()
        {
            return this.partitions.Length;
        }

        //****************************************************
        // Method: GetPartitionAddress
        //
        // Purpose: Returns the absolute starting address of the
        //          partition with the given partition number.
        //****************************************************
        public int GetPartitionAddress(int partitionNum)
        {
            try
            {
                return this.partitions[partitionNum].AbsoluteAddress;
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }
        }

        //****************************************************
        // Method: GetPartitionSize
        //
        // Purpose: Returns the size of the partition with the
        //          given partition number.
        //****************************************************
        public int GetPartitionSize(int partitionNum)
        {
            try
            {
                return this.partitions[partitionNum].Size;
            }
            catch (IndexOutOfRangeException)
            {
                return -1;
            }
        }

        //****************************************************
        // Method: IsPartitionInUse
        //
        // Purpose: Returns true if the partition with the given
        //          partition number is in use, otherwise false.
        //****************************************************
        public bool IsPartitionInUse(int partitionNum)
        {
            return this.partitions[partitionNum].IsUsed;
        }

        //****************************************************
        // Method: SetNumPartitions
        //
        // Purpose: Sets the number of partitions in the system to
        //          the specified number. All memory is reset in the
        //          process. Returns the number of partitions.
        //****************************************************
        public int SetNumPartitions(int numPartitions)
        {
            this.operatingSystemSettings.NumPartitions = numPartitions;

            int userAreaSize = operatingSystemSettings.NumPartitions * 100;
            
            // Re-initialize memory with new settings.
            InitMemory(14, 5, userAreaSize, operatingSystemSettings);

            return operatingSystemSettings.NumPartitions;
        }

        //****************************************************
        // Method: SetPartitionSize
        //
        // Purpose: Sets the size of the partition with the specified
        //          number to the given size. Not implemented.
        //****************************************************
        public int SetPartitionSize(int partitionNum, int size)
        {
            // Not implemented
            throw new NotImplementedException();
        }

        //****************************************************
        // Method: UsedPartitions
        //
        // Purpose: Returns the number of partitions in use.
        //****************************************************
        public int UsedPartitions()
        {
            return this.partitions.Length - FreePartitions();
        }

        //****************************************************
        // Method: Allocate
        //
        // Purpose: Returns the address of the next available
        //          user partition that can fit the requested memory size.
        //****************************************************
        public int Allocate(int size, IProcessControlBlock pcb)
        {
            return MyFixedPartitionMemoryManager.addressOfNextAvailablePartition(size, this.userPartitionPointers);
        }

        //****************************************************
        // Method: AllocateOSReserved
        //
        // Purpose: Returns the address of the next available OS
        //          partition that can fit the requested memory size.
        //****************************************************
        public int AllocateOSReserved(int size, IProcessControlBlock pcb)
        {
            return MyFixedPartitionMemoryManager.addressOfNextAvailablePartition(size, this.osPartitionPointers);
        }

        //****************************************************
        // Method: ICPU
        //
        // Purpose: Property gets or sets the system's CPU.
        //****************************************************
        public ICPU CPU
        {
            get
            {
                return this.cpu;
            }
            set
            {
                this.cpu = value;
            }
        }

        //****************************************************
        // Method: CurrentLargestAllocationSize
        //
        // Purpose: Returns the size of the largest partition
        //          that is currently allocated.
        //****************************************************
        public int CurrentLargestAllocationSize()
        {
            int largestSize = 0;

            foreach (Partition partition in this.partitions)
            {
                if (partition.IsUsed && (partition.Size > largestSize))
                {
                    largestSize = partition.Size;
                }
            }
            
            return largestSize != 0 ? largestSize : operatingSystemSettings.PartitionSize;
        }

        //****************************************************
        // Method: Deallocate
        //
        // Purpose: Allows the user partition at the given address
        //          to be allocated with a new process.
        //****************************************************
        public int Deallocate(int addr)
        {
            return MyFixedPartitionMemoryManager.deallocatePartition(addr, this.partitions);
        }

        //****************************************************
        // Method: DeallocateOSReserved
        //
        // Purpose: Allows the OS partition at the given address
        //          to be allocated with a new process.
        //****************************************************
        public int DeallocateOSReserved(int addr)
        {
            return MyFixedPartitionMemoryManager.deallocatePartition(addr, this.osPartitionPointers);
        }

        //****************************************************
        // Method: FreeMemory
        //
        // Purpose: Returns the number of free partitions.
        //****************************************************
        public int FreeMemory()
        {
            int free = 0;

            foreach (Partition partition in this.partitions)
            {
                if(!partition.IsUsed)
                {
                    free += partition.Size;
                }
            }

            return free;
        }

        //****************************************************
        // Method: GetDataAbsolute
        //
        // Purpose: Gets the data item from a partition at the
        //          given absolute address.
        //****************************************************
        public IDataItem GetDataAbsolute(int absoluteAddr)
        {
            Partition partition = MyFixedPartitionMemoryManager.findPartition(this.partitions, absoluteAddr);

            if (partition != null)
            {
                return partition.getData(absoluteAddr);
            }

            return null;
        }

        //****************************************************
        // Method: GetDataRelative
        //
        // Purpose: Gets the data item from a partition using the
        //          given relative address.
        //****************************************************
        public IDataItem GetDataRelative(int relativeAddr)
        {
            return this.GetDataAbsolute(this.cpu.Base.Num + relativeAddr);
        }

        //****************************************************
        // Method: GetSize
        //
        // Purpose: Gets the sum total size of memory.
        //****************************************************
        public int GetSize()
        {
            int size = 0;

            foreach (Partition partition in this.partitions)
            {
                size += partition.Size;
            }

            return size;
        }

        //****************************************************
        // Method: InitMemory
        //
        // Purpose: Adds the given number of partitions to the system
        //          for both user and OS reserved space, and sets
        //          the memory manager to a default running state.
        //****************************************************
        public int InitMemory(int pcbSize, int maxNumPcb, int userAreaSize, IOperatingSystemSettings osSettings)
        {
            // pcbsize = Size of operating system partition
            // maxNumPcb = Number of operating system partitions
            
            this.operatingSystemSettings = osSettings;
            this.partitionCounter = -1;

            // Total number of partitions is equal to number of user partitions (osSettings.NumPartitions) + number of OS reserved partitions (maxNumPcb).
            this.partitions = new Partition[osSettings.NumPartitions + maxNumPcb];

            InitOSReservedSpace(pcbSize, maxNumPcb);

            int[] userPartitionIndexes = new int[osSettings.NumPartitions];

            for (int partitionNumber = 0; partitionNumber < osSettings.NumPartitions; ++partitionNumber)
            {
                int newPartitionIndex = AddPartition(osSettings.PartitionSize);

                if (newPartitionIndex == -1)
                {
                    return -1;
                }
                else
                {
                    userPartitionIndexes[partitionNumber] = newPartitionIndex;
                }
            }

            this.userPartitionPointers = new Partition[userPartitionIndexes.Length];

            for (int i = 0; i < userPartitionIndexes.Length; ++i)
            {
                int userPartitionIndex = userPartitionIndexes[i];
                this.userPartitionPointers[i] = this.partitions[userPartitionIndex];
            }

            return 0;
        }

        //****************************************************
        // Method: InitOSReservedSpace
        //
        // Purpose: Called by InitMemory to add the requested number
        //          of OS reserved partitions to the system.
        //****************************************************
        public int InitOSReservedSpace(int pcbSize, int maxNumPcb)
        {
            int[] osPartitionIndexes = new int[maxNumPcb];

            // Add partitions with size of the processor control block
            // until max number of PCBs is reached.
            // Partition index is stored to keep track of OS partitions.
            for (int OSpcb = 0; OSpcb < maxNumPcb; ++OSpcb)
            {
                int newPartitionIndex = AddPartition(pcbSize);

                if (newPartitionIndex == -1)
                {
                    return -1;
                }
                else
                {
                    osPartitionIndexes[OSpcb] = newPartitionIndex;
                }
            }

            // Use the stored OS partition indexes to create array
            // pointing to OS partitions in the main partitions array
            this.osPartitionPointers = new Partition[osPartitionIndexes.Length];

            for (int i = 0; i < osPartitionIndexes.Length; ++i)
            {
                int osPartitionIndex = osPartitionIndexes[i];
                this.osPartitionPointers[i] = this.partitions[osPartitionIndex];
            }

            this.userMemoryPartitionsOffset = maxNumPcb;

            // User memory starts at the end of OS reserved space.
            // Number of of spaces reserved = PCB Size * Max Number of PCBs.
            // OS reserved space indexes are 0 through (PCB Size * Max Number of PCBs) - 1.
            this.userMemoryStartingAddress = pcbSize * maxNumPcb;

            return this.userMemoryStartingAddress;
        }

        //****************************************************
        // Method: MaximumPossibleAllocationSize
        //
        // Purpose: Returns the size of the largest free partition.
        //****************************************************
        public int MaximumPossibleAllocationSize()
        {
            int largestSize = 0;

            foreach (Partition partition in this.partitions)
            {
                if (!partition.IsUsed && (partition.Size > largestSize))
                {
                    largestSize = partition.Size;
                }
            }

            return largestSize != 0 ? largestSize : operatingSystemSettings.PartitionSize;
        }

        //****************************************************
        // Method: MemoryType
        //
        // Purpose: Gets/sets the memory scheme used by this memory manager.
        //****************************************************
        public OperatingSystemMemoryTypes MemoryType
        {
            get
            {
                return this.memoryType;
            }
            set
            {
                this.memoryType = value;
            }
        }

        //****************************************************
        // Method: ProcessorManager
        //
        // Purpose: Gets/sets the processor manager used by this
        //          memory manager.
        //****************************************************
        public IProcessorManager ProcessorManager
        {
            get
            {
                return this.processorManager;
            }
            set
            {
                this.processorManager = value;
            }
        }

        //****************************************************
        // Method: SetDataAbsolute
        //
        // Purpose: Sets memory at the specified absolute address
        //          to the given data item.
        //****************************************************
        public int SetDataAbsolute(int absoluteAddr, IDataItem data)
        {
            Partition partition = MyFixedPartitionMemoryManager.findPartition(this.partitions, absoluteAddr);

            if (partition != null)
            {
                return partition.setData(absoluteAddr, data);
            }

            return -1;
        }

        //****************************************************
        // Method: SetDataRelative
        //
        // Purpose: Sets memory at the specified relative address
        //          to the given data item.
        //****************************************************
        public int SetDataRelative(int relativeAddr, IDataItem data)
        {
            return this.SetDataAbsolute(this.cpu.Base.Num + relativeAddr, data);
        }

        //****************************************************
        // Method: SetSize
        //
        // Purpose: Not implemented.
        //****************************************************
        public int SetSize(int size)
        {
            // Not implemented
            return -1;
        }

        //****************************************************
        // Method: UsedMemory
        //
        // Purpose: Returns the size of used memory.
        //****************************************************
        public int UsedMemory()
        {
            return this.GetSize() - this.FreeMemory();
        }

        #endregion

        #region Helper Methods

        //****************************************************
        // Method: findPartition
        //
        // Purpose: Finds a partition that contains the given memory address
        //          in the specified array of Partitions. Returns a Partition
        //          object that contains the memory address, and null if no parition
        //          is found or if there is an error.
        //****************************************************
        protected static Partition findPartition(Partition[] inPartitions, int byAddress)
        {
            foreach (Partition partition in inPartitions)
            {
                if (partition.containsAddress(byAddress))
                {
                    return partition;
                }
            }

            return null;
        }

        //****************************************************
        // Method: calculatePartitionAbsoluteStartingAddress
        //
        // Purpose: Calculates the absolute starting address of a partition.
        //          Returns an integer value of the starting address of
        //          the given partition number.
        //****************************************************
        protected static int calculatePartitionAbsoluteStartingAddress(int partitionNumber, int partitionSize, int userMemoryStartingAddress, int userMemoryPartitionsOffset)
        {
            return userMemoryStartingAddress + (partitionSize * (partitionNumber - userMemoryPartitionsOffset));
        }

        //****************************************************
        // Method: addressOfNextAvailablePartition
        //
        // Purpose: Gets the address of the next available partition
        //          in an array of partitions that can hold the specified size.
        //          Returns the absolute address of the beginning of the partition,
        //          or -1 on error.
        //****************************************************
        protected static int addressOfNextAvailablePartition(int forSize, Partition[] inPartitions)
        {
            foreach (Partition partition in inPartitions)
            {
                if (partition.canAllocate(forSize))
                {
                    return partition.AbsoluteAddress;
                }
            }

            return -1;
        }

        //****************************************************
        // Method: deallocatePartition
        //
        // Purpose: Deallocates a partition in the given array
        //          of partitions at the specified address.
        //          Returns 0 if successful, or -1 on error.
        //****************************************************
        protected static int deallocatePartition(int atAddress, Partition[] inPartitions)
        {
            Partition partition = MyFixedPartitionMemoryManager.findPartition(inPartitions, atAddress);

            if (partition != null)
            {
                return partition.deallocate();
            }

            return -1;
        }

        #endregion
    }
}
