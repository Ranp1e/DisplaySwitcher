Dim fso, scriptDir, exePath, w, h, r, cmd, objShell

Set fso = CreateObject("Scripting.FileSystemObject")
scriptDir = fso.GetParentFolderName(WScript.ScriptFullName)
exePath = scriptDir & "\QRes.exe"

w = WScript.Arguments(0)
h = WScript.Arguments(1)
If WScript.Arguments.Count > 2 Then
    r = WScript.Arguments(2)
Else
    r = ""
End If

cmd = """" & exePath & """ /x:" & w & " /y:" & h
If r <> "" Then
    cmd = cmd & " /r:" & r
End If

Set objShell = CreateObject("WScript.Shell")
objShell.Run cmd, 0, True
