AccenType
============
Latin-based languages use the same alphabet as English with only difference being the accents of letters (e.g. for Spanish, Italian, German, Vietnamese, French, etc...). On an English keyboard, user is required to type a combination of keys to create an "accented" letter, which can be cumbersome. For example: typing "aa" creates "â", or "ALT + 142" creates "Ä". Via Machine Learning techniques, AccenType can predict accents with high accuracy without requiring users to enter the long combined sequence of keys. This could save at least 50% time in typing documents, emails, etc... For a live demo, please visit: http://accentype.azurewebsites.net/

Build Pre-requisites: Visual Studio 2013 Update 5.

AccenType is trained and tested on a dataset collected from webpages consisting mostly of news content, achieving > 95% word accuracy. For benchmarking and experimentation purposes, these are available for download here: [train](https://www.dropbox.com/s/02mfibeoiivrhbv/batches_train.txt?dl=0) and [test](https://www.dropbox.com/s/faeifh4k627vdoq/batches_test.txt?dl=0).

The backend prediction cloud service runs on Microsoft Azure platform and uses UDP sockets for low-latency data exchange. External queries can be made against this service to retrieve predictions. For sample code on how this is done, see [C# sample](https://github.com/accentype/accentype-learn/blob/master/AccenTypeConsole/UDPTest.cs#L15) or [Java sample](https://github.com/accentype/accentype-android/blob/master/app%2Fsrc%2Fmain%2Fjava%2Fcom%2Faccentype%2Fandroid%2Fsoftkeyboard%2FSoftKeyboard.java#L1127).

This code is released under MIT license.
