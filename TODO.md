# TODO list

This is yet unsorted and unpriorized, and most issues are still open for discussion.

If there are already better, mature solutions with acceptable license available, refer to them instead of writing our own. :-)

General: should decoupling via Task.Run / ForceAsync / ForceSerial be better done in a special stream?

## General:
- Test Coverage.

## TeaStream
- Seek support when all underlying streams support seek
- Timeout support
- Do we need to support CreateWaitHandle()?
- Use "Task.Run" to decouple streams which use high CPU load (e. g. CryptoStream).
- - Not sure yet how to do this without introducing too much overhead for the other baseStreams.
- - Maybe this is better done using some dedicated stream wrapper?
- Do we gain anything by implementing our own CopyToAsync()?

## FilterStream
- Filter all bytes through given Filter delegates.

## CoffeStream
- Readable stream wrapper who replicates all data into a TeaStream.

## PipeStream
- Connect together a readable and a writable stream

## NullStream
- Stream which provides as many 0 bytes as you want, and just drops data on write.

## SpillStream