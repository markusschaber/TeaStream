/* TeaStream Project - Stream Utilities to replicate, duplicate and process Streams in .NET (Core)
 * 
 * Copyright © 2019 Markus Schaber
 * 
 * Licensed under MIT License, see LICENSE.txt file in top level project directory. 
 */

namespace TeaStream
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    [Flags]
    public enum TeaFlags
    {
        None = 0,

        /// <summary>
        /// Force maximum parallelization even at the cost of scheduling additional
        /// tasks into the thread pool with <see cref="Task.Run"/>.
        /// </summary>
        ForceParallel = 1,

        /// <summary>
        /// Force serialization, even for Async calls.
        /// </summary>
        ForceSerial = 2,
    }
}
