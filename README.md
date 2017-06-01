# Bye

Bye uninstalls programs.

It requires Windows and the .NET framework. 

Add `bye.exe` to your path<sup>1</sup>, and then use like so:
`bye someannoyingprogram`

If `bye` is not sure what program to uninstall, it will prompt you:

````
C:\Windows\system32>bye micro
Which?
[0] Blender
[1] Microsoft Visual C++ 2005 Redistributable`
[...snip...]
````

Type the number and hit enter to proceed.

Binaries available [here](https://github.com/cwaldren/Bye/releases/download/v0.1.0/Bye.zip); if they don't work then compile from source. Requires elevated permissions to run (for uninstalling apps).

[1] If using Win10: 
1. Search `env` and "Edit the system environment variables" should popup
2. Open it, click "Environment Variables", 
3. Select `Path`, hit Edit
4. Hit New, then paste in the directory that contains bye.exe
5. Now you can open a command prompt and type `bye program`
