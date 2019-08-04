# TODO list

This is yet unsorted and unpriorized, and most issues are still open for discussion.

If there are already better, mature solutions with acceptable license available, refer to them instead of writing our own. :-)

## General:
- Test Coverage.
- NuGet Packaging
- Checking whether other/older target frameworks can be supported
- should forced decoupling via Task.Run be better done in a special stream? Would unclutter TeaStream and allow for finer grained control.

## TeaStream
- Seek support when all underlying streams support seek
- Timeout support
- Do we need to support CreateWaitHandle()?
- Use "Task.Run" to decouple streams which use high CPU load (e. g. CryptoStream).
- - Not sure yet how to do this without introducing too much overhead for the other baseStreams.
- - Maybe this is better done using some dedicated stream wrapper?
- Do we gain anything by implementing our own CopyToAsync()?
- Check whether ForceAsync can lead to dead locks on the UI thread (maybe ConfigureAwait(false) or something is necessary? Or is this the callers responsibility?)

## FilterStream
- Filter all bytes through given Filter delegates.

## CoffeStream
- Readable stream wrapper who sidechannels all data into a writeable (Tea)Stream.

## PipeStream
- Connect together a readable and a writable stream
- May need a better name or be obsolete due to existing PipeStreams https://docs.microsoft.com/de-de/dotnet/standard/io/pipe-operations

## ClearStream
- Stream which provides as many 0 bytes as you want, and just drops data on write, use for testing.

## SpillStream
- Is multi-level spilling necessary? (More than the two layers MemoryStream and FileStream)?
- Compression (will disable seeking, except to seek back to the beginning for reading again).