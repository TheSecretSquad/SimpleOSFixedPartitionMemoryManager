using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleOSInterfaces;

namespace FixedPartitionMemoryManager
{
    public class Partition
    {
        #region Private Variables

        private int partitionNumber;
        private IMemoryBlock memoryBlock;
        private int absoluteStartingAddress;
        private bool isUsed;

        #endregion

        //****************************************************
        // Method: Partition
        //
        // Purpose: Constructor initializes the partition.
        //****************************************************
        public Partition(int partitionNumber, int absoluteStartingAddress, IMemoryBlock memoryBlock)
        {
            this.partitionNumber = partitionNumber;
            this.absoluteStartingAddress = absoluteStartingAddress;
            this.memoryBlock = memoryBlock;
        }

        #region Methods

        //****************************************************
        // Method: getData
        //
        // Purpose: Get the data at the given absolute address.
        //          Returns an IDataItem, or null on error.
        //****************************************************
        public IDataItem getData(int absoluteAddress)
        {
            int resolvedAddress = resolveAbsoluteAddress(absoluteAddress);

            return this.memoryBlock.GetData(resolvedAddress);
        }

        //****************************************************
        // Method: setData
        //
        // Purpose: Sets the data at the given absolute address.
        //          Returns 0 on success, or -1 on error.
        //****************************************************
        public int setData(int absoluteAddress, IDataItem data)
        {
            int resolvedAddress = resolveAbsoluteAddress(absoluteAddress);

            if (this.IsUsed == false)
            {
                this.isUsed = true;
            }

            if (this.memoryBlock.SetData(resolvedAddress, data) == -1)
            {
                return -1;
            }

            return 0;
        }

        //****************************************************
        // Method: deallocate
        //
        // Purpose: Deallocates the partition. Returns 0 on
        //          success, or -1 on error.
        //****************************************************
        public int deallocate()
        {
            this.isUsed = false;

            if (this.IsUsed == false)
            {
                return 0;
            }
            else
            {
                return -1;
            }
        }

        //****************************************************
        // Method: containsAddress
        //
        // Purpose: Determines whether the given address is
        //          within the partition. Returns true if the
        //          address is contained within the partition, otherwise false.
        //****************************************************
        public bool containsAddress(int absoluteAddress)
        {
            int endingAddress = absoluteStartingAddress + this.Size - 1;

            if((this.AbsoluteAddress <= absoluteAddress) && (absoluteAddress <= endingAddress))
            {
                return true; 
            }

            return false;
        }

        //****************************************************
        // Method: canAllocate
        //
        // Purpose: Determines whether a partition can be
        //          allocated with the given memory size.
        //          Returns true of the partition can be
        //          allocated, otherwise false.
        //****************************************************
        public bool canAllocate(int withSize)
        {
            // Is the partition available, and can it fit the given size?
            return !this.IsUsed && this.Size >= withSize;
        }

        //****************************************************
        // Method: resolveAbsoluteAddress
        //
        // Purpose: Calculates a relative address within
        //          the partition from the given absolute address.
        //          Returns an integer value of the address
        //          within the partition.
        //****************************************************
        private int resolveAbsoluteAddress(int absoluteAddress)
        {
            return absoluteAddress % this.Size;
        }

        #endregion

        #region Properties

        //****************************************************
        // Method: Size Property
        //
        // Purpose: Gets/sets the size of the partition's 
        //          memory block;
        //****************************************************
        public int Size
        {
            get
            {
                return this.memoryBlock.GetSize();
            }
            set
            {
                for (int item = 0; item < this.Size; ++item)
                {
                    memoryBlock.SetData(item, null);
                }

                this.memoryBlock.SetSize(value);
            }
        }

        //****************************************************
        // Method: IsUsed Property
        //
        // Purpose: Returns true if the partition is in use,
        //          otherwise false.
        //****************************************************
        public bool IsUsed
        {
            get
            {
                return this.isUsed;
            }
        }

        //****************************************************
        // Method: AbsoluteAddress Property
        //
        // Purpose: Returns the absolute starting address
        //          for this partition.
        //****************************************************
        public int AbsoluteAddress
        {
            get
            {
                return this.absoluteStartingAddress;
            }
        }

    #endregion
    }
}
