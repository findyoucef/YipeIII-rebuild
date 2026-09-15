/* Address: 004042e0; Ghidra name: Yipe_CalculateMonsterAttackDamage */

/* Called for each living opponent. Returns integer damage to the player; callers use the value
   arithmetically. The former pointer return was a decompiler inference error. */

int __thiscall Yipe_CalculateMonsterAttackDamage(void *this,int param_1)

{
  void *pvVar1;
  int iVar2;
  int iVar3;
  int iVar4;
  int iVar5;
  int iVar6;
  int iVar7;
  int iVar8;
  int iVar9;
  int iVar10;
  int iVar11;
  HBRUSH pHVar12;
  int iVar13;
  undefined **ppuVar14;
  int iVar15;
  int iVar16;
  undefined **local_17c;
  undefined4 local_178;
  int local_174;
  void *local_170;
  int local_16c [84];
  tagRECT local_1c;
  void *local_c;
  undefined1 *puStack_8;
  undefined4 local_4;
  
  local_4 = 0xffffffff;
  puStack_8 = &LAB_00486b86;
  local_c = ExceptionList;
  local_16c[0] = 1;
  local_16c[2] = 1;
  local_16c[4] = 1;
  local_16c[6] = 1;
  local_16c[7] = 4;
  local_16c[8] = 1;
  local_16c[9] = 4;
  local_16c[0xb] = 4;
  local_16c[0x18] = 4;
  local_16c[0x1a] = 4;
  local_16c[1] = 2;
  local_16c[10] = 2;
  local_16c[0xc] = 2;
  local_16c[0x14] = 2;
  local_16c[0x15] = 8;
  local_16c[0x17] = 8;
  local_16c[0x1b] = 8;
  local_16c[0x1d] = 8;
  local_16c[0x1f] = 9;
  local_16c[0x21] = 9;
  local_16c[0x30] = 8;
  local_16c[0x32] = 8;
  local_16c[0x34] = 8;
  local_16c[0x36] = 8;
  local_16c[0x38] = 9;
  local_16c[0x3a] = 9;
  local_16c[0x3c] = 9;
  local_17c = (undefined **)0x0;
  local_16c[3] = 3;
  local_16c[5] = 3;
  local_16c[0xd] = 5;
  local_16c[0xe] = 3;
  local_16c[0xf] = 5;
  local_16c[0x10] = 3;
  local_16c[0x11] = 6;
  local_16c[0x12] = 3;
  local_16c[0x13] = 7;
  local_16c[0x16] = 3;
  local_16c[0x19] = 7;
  local_16c[0x1c] = 5;
  local_16c[0x1e] = 5;
  local_16c[0x20] = 6;
  local_16c[0x22] = 5;
  local_16c[0x23] = 10;
  local_16c[0x24] = 6;
  local_16c[0x25] = 10;
  local_16c[0x26] = 7;
  local_16c[0x27] = 10;
  local_16c[0x28] = 7;
  local_16c[0x29] = 0xb;
  local_16c[0x2a] = 7;
  local_16c[0x2b] = 0xc;
  local_16c[0x2c] = 7;
  local_16c[0x2d] = 0xd;
  local_16c[0x2e] = 7;
  local_16c[0x2f] = 0xe;
  local_16c[0x31] = 0xd;
  local_16c[0x33] = 0xe;
  local_16c[0x35] = 0xf;
  local_16c[0x37] = 0x10;
  local_16c[0x39] = 0xf;
  local_16c[0x3b] = 0x10;
  local_16c[0x3d] = 0x11;
  local_16c[0x3e] = 10;
  local_16c[0x3f] = 0x11;
  local_16c[0x46] = 7;
  local_16c[0x49] = 0xf;
  local_16c[0x4c] = 0xf;
  local_16c[0x52] = 0xf;
  iVar3 = *(int *)((int)this + 0x2f0);
  local_16c[0x50] = 0x16;
  local_16c[0x53] = 0x16;
  local_174 = (int)*(short *)(iVar3 + 0x6ac);
  local_16c[0x45] = 0x14;
  local_16c[0x4b] = 0x14;
  local_16c[0x4e] = 0x14;
  local_16c[0x40] = 10;
  local_16c[0x41] = 0x12;
  local_16c[0x42] = 10;
  iVar16 = *(int *)((int)this + (param_1 + 0x1b) * 0x1c);
  local_16c[0x43] = 0x13;
  iVar2 = *(int *)(iVar3 + 0x7c);
  local_16c[0x44] = 10;
  local_16c[0x47] = 0xb;
  local_16c[0x48] = 10;
  local_16c[0x4a] = 0xc;
  local_16c[0x4d] = 0x19;
  local_16c[0x4f] = 0x1e;
  local_16c[0x51] = 0x20;
  iVar13 = 3;
  if (4 < iVar2) {
    if (iVar2 < 10) {
      iVar13 = 6;
    }
    else {
      iVar13 = 10;
      if (0xe < iVar2) {
        if (iVar2 < 0x14) {
          iVar13 = 0xf;
        }
        else if (iVar2 < 0x1c) {
          iVar13 = 0x14;
        }
        else {
          iVar13 = ((0x25 < iVar2) - 1 & 0xfffffff9) + 0x23;
        }
      }
    }
  }
  iVar15 = *(int *)(iVar3 + 0x58);
  ExceptionList = &local_c;
  iVar3 = FUN_00406fc0(*(int *)(iVar3 + 0x84) * 2);
  iVar4 = _rand();
  iVar5 = _rand();
  pvVar1 = (void *)((int)this + param_1 * 0x1c);
  local_170 = pvVar1;
  iVar6 = FUN_00406fc0(iVar16);
  iVar7 = FUN_00406fc0(*(int *)((int)pvVar1 + 0x308));
  iVar7 = iVar7 + local_174;
  iVar8 = FUN_00406fc0(*(int *)((int)pvVar1 + 0x308));
  iVar9 = FUN_00404e00(this,param_1);
  iVar9 = FUN_00406fc0(iVar9 + 2);
  iVar10 = FUN_004451a0(*(int *)((int)this + 0x2f0) + 0x50);
  iVar11 = FUN_00406fc0(iVar10 + 1);
  iVar15 = (iVar8 + iVar7 + iVar6 + iVar9) - (iVar3 + iVar4 % iVar15 + iVar5 % iVar13 + iVar11);
  iVar3 = _rand();
  iVar13 = 0x5c;
  if (5 < iVar2) {
    iVar13 = 0x57;
  }
  if (0xc < iVar2) {
    iVar13 = 0x4b;
  }
  if (0x17 < iVar2) {
    iVar13 = 0x3c;
  }
  if ((iVar15 < 1) && (iVar13 - iVar16 / 2 < iVar3 % 100)) {
    iVar15 = 1;
  }
  else {
    ppuVar14 = local_17c;
    if (iVar15 < 1) goto LAB_004048a2;
  }
  iVar3 = local_16c[iVar16 * 2];
  iVar13 = FUN_00406fc0((local_16c[iVar16 * 2 + 1] - iVar3) + 1);
  pvVar1 = local_170;
  iVar4 = FUN_00406fc0(*(int *)((int)local_170 + 0x304) + 1);
  ppuVar14 = (undefined **)(iVar13 + iVar3 + iVar4);
  if ((iVar16 == 0x29) && (0 < *(short *)(*(int *)((int)this + 0x2f0) + 0x6b2))) {
    iVar3 = FUN_00406fc0(5);
    ppuVar14 = (undefined **)((int)ppuVar14 + iVar3 + 5);
  }
  if (0xf < iVar15) {
    iVar3 = _rand();
    ppuVar14 = (undefined **)((int)ppuVar14 + iVar3 % *(int *)((int)pvVar1 + 0x304));
  }
  if (local_174 == 3) {
    ppuVar14 = (undefined **)((int)ppuVar14 + 1);
  }
  else if ((local_174 == 1) && (2 < (int)ppuVar14)) {
    ppuVar14 = (undefined **)((int)ppuVar14 + -1);
  }
  if ((((iVar16 == 7) || (iVar16 == 0xd)) || (iVar16 == 0xf)) && (9 < iVar2)) {
    ppuVar14 = (undefined **)((int)ppuVar14 + local_174);
  }
  if (0x22 < iVar16) {
    ppuVar14 = (undefined **)((int)ppuVar14 + local_174);
  }
  if ((3 < (int)ppuVar14) && (iVar2 < 3)) {
    ppuVar14 = (undefined **)((int)ppuVar14 + -1);
  }
  if (2 < (int)ppuVar14) {
    if ((iVar2 == 1) && (*(int *)(*(int *)((int)this + 0x2f0) + 100) < 6)) {
      ppuVar14 = (undefined **)0x1;
      goto LAB_004048a2;
    }
    if ((iVar2 < 4) && (*(int *)(*(int *)((int)this + 0x2f0) + 100) < 6)) {
      ppuVar14 = (undefined **)((int)ppuVar14 + -1);
    }
  }
  if ((10 < (int)ppuVar14) && (10 < iVar10)) {
    iVar3 = FUN_00406fc0(6);
    ppuVar14 = (undefined **)((int)ppuVar14 + (-1 - iVar3));
  }
  if ((0x14 < (int)ppuVar14) && (0x14 < iVar10)) {
    iVar3 = FUN_00406fc0(6);
    ppuVar14 = (undefined **)((int)ppuVar14 + (-2 - iVar3));
  }
  if ((0x1c < (int)ppuVar14) && (0x1c < iVar10)) {
    iVar3 = FUN_00406fc0(6);
    ppuVar14 = (undefined **)((int)ppuVar14 + (-3 - iVar3));
  }
  if ((10 < (int)ppuVar14) && (*(int *)(*(int *)((int)this + 0x2f0) + 100) < 0x23)) {
    ppuVar14 = (undefined **)((int)ppuVar14 + -3);
  }
  if ((0x14 < (int)ppuVar14) && (*(int *)(*(int *)((int)this + 0x2f0) + 100) < 0x32)) {
    ppuVar14 = (undefined **)((int)ppuVar14 + -5);
  }
  if ((iVar2 == 1) && (5 < (int)ppuVar14)) {
    ppuVar14 = (undefined **)0x5;
  }
LAB_004048a2:
  iVar3 = *(int *)((int)this + 0x2f0);
  iVar16 = *(int *)(iVar3 + 200);
  if ((*(int *)(iVar3 + 0xc4) == 3) && (0 < iVar16)) {
    ppuVar14 = (undefined **)0x0;
    iVar16 = iVar16 + -1;
    *(int *)(iVar3 + 200) = iVar16;
    if (iVar16 < 1) {
      FUN_0044dcb0((void *)((int)this + 0x220),0);
      RedrawWindow(*(HWND *)((int)this + 0x23c),(RECT *)0x0,(HRGN)0x0,0x105);
    }
  }
  if ((*(int *)(*(int *)((int)this + 0x2f0) + 0xc4) == 4) && (0 < iVar16)) {
    if (2 < (int)ppuVar14) {
      ppuVar14 = (undefined **)((int)ppuVar14 + -1);
    }
    *(int *)(*(int *)((int)this + 0x2f0) + 200) = iVar16 + -1;
    if (iVar16 + -1 < 1) {
      FUN_00446cf0(*(int *)((int)this + 0x2f0) + 0x50);
      GetDC(*(HWND *)((int)this + 0x1c));
      iVar3 = FUN_0046ff53();
      iVar16 = FUN_0046c72d(this,0x440);
      GetWindowRect(*(HWND *)(iVar16 + 0x1c),&local_1c);
      FUN_004706b8(this,(LPPOINT)&local_1c);
      local_17c = &PTR_FUN_0048e878;
      local_178 = 0;
      local_4 = 0;
      pHVar12 = CreateSolidBrush(0);
      FUN_00470aaa(&local_17c,(uint)pHVar12);
      GetCurrentObject(*(HDC *)(iVar3 + 8),2);
      iVar16 = FUN_00470a95();
      if (iVar16 == 0) {
        pHVar12 = (HBRUSH)0x0;
      }
      else {
        pHVar12 = *(HBRUSH *)(iVar16 + 4);
      }
      FrameRect(*(HDC *)(iVar3 + 4),&local_1c,pHVar12);
      ReleaseDC(*(HWND *)((int)this + 0x1c),*(HDC *)(iVar3 + 4));
      local_17c = &PTR_FUN_0048e95c;
      local_4 = 1;
      FUN_00470b01((int)&local_17c);
    }
  }
  ExceptionList = local_c;
  return (int)ppuVar14;
}


