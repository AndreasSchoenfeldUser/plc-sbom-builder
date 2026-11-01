PROGRAM PLC_PRG
VAR
    bStart : BOOL;
    nTakt : INT;
END_VAR

IF bStart THEN
    nTakt := nTakt + 1;
END_IF
