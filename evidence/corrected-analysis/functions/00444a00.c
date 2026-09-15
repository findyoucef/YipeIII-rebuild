/* Address: 00444a00; Ghidra name: Yipe_AdjustPlayerHP */

/* Analyst-assigned name; not an original source symbol.
   Adds a signed delta to current HP at offset 0x14, caps at maximum HP at 0x18, and returns a
   live/dead indicator in the low byte. */

bool __thiscall Yipe_AdjustPlayerHP(void *this,int param_1)

{
  int iVar1;
  
  iVar1 = *(int *)((int)this + 0x14) + param_1;
  *(int *)((int)this + 0x14) = iVar1;
  if (*(int *)((int)this + 0x18) < iVar1) {
    *(int *)((int)this + 0x14) = *(int *)((int)this + 0x18);
  }
  return 0 < *(int *)((int)this + 0x14);
}


