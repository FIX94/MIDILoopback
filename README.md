# MIDILoopback
This is a small C# project written for windows vista or newer where it is not possible to select a default MIDI device anymore with consistency.  
To compile this project you will have to download and install the VirtualMIDI SDK and also copy teVirtualMIDI.cs from its Cs-Binding folder into this project folder.  
The application will directly go into your system tray which is handy for having it autostart with windows.  
Starting the application will overwrite the current MIDI driver in the windows registry with VirtualMIDI until you close the application again.  
This means all the default MIDI I/O will be sent directly to this application.  
All of the output of that port will be sent back into its input, so you can play back something and use it as input source in another program.  
