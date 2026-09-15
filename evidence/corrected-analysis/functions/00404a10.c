/* Address: 00404a10; Ghidra name: Yipe_CalculatePlayerAttackDamage */

/* Analyst-assigned name; not an original source symbol.
   Returns zero for a miss or a positive damage amount, which the combat-turn routine subtracts from
   the selected opponent's HP. */

int __fastcall Yipe_CalculatePlayerAttackDamage(void *param_1)

{
  int iVar1;
  int iVar2;
  int iVar3;
  int iVar4;
  int iVar5;
  int iVar6;
  int iVar7;
  int iVar8;
  int iVar9;
  int iVar10;
  uint local_24;
  int local_18;
  int local_14;
  int local_10;
  int local_c;
  int local_8;
  int local_4;
  
  iVar3 = *(int *)(*(int *)((int)param_1 + 0x2f0) + 0x80);
  FUN_00417ab0(&local_18,&local_8,iVar3);
  iVar9 = *(int *)((int)param_1 + 0x2f0);
  local_14 = (int)*(short *)(iVar9 + 0x6ac);
  iVar1 = *(int *)(iVar9 + 0x7c);
  iVar9 = *(int *)(iVar9 + 0x60);
  iVar10 = *(int *)((int)param_1 + (*(int *)((int)param_1 + 0x348) + 0x1b) * 0x1c);
  local_4 = iVar10;
  iVar2 = _rand();
  local_c = _rand();
  local_c = local_c % (iVar3 + 1);
  if (iVar1 < 0x29) {
    iVar3 = _rand();
    iVar3 = iVar3 % iVar1;
  }
  else {
    iVar3 = _rand();
    iVar3 = iVar3 % 0x28;
  }
  iVar4 = FUN_00404d20(param_1,iVar10);
  iVar5 = _rand();
  iVar6 = FUN_004451e0(*(int *)((int)param_1 + 0x2f0) + 0x50);
  local_10 = _rand();
  local_10 = local_10 % (iVar6 + 1);
  iVar6 = FUN_00404da0(param_1,iVar10);
  iVar7 = _rand();
  if (local_14 == 1) {
    iVar8 = 5;
  }
  else if (local_14 == 2) {
    iVar8 = 3;
  }
  else {
    iVar8 = 1;
    if (local_14 != 3) {
      iVar8 = local_4;
    }
  }
  iVar4 = (iVar8 - iVar7 % (iVar6 + 1)) + local_10 + iVar5 % (iVar4 + 1) + iVar3 + 1 + local_c;
  iVar3 = FUN_00406fc0(*(int *)((int)param_1 + *(int *)((int)param_1 + 0x348) * 0x1c + 0x304));
  iVar5 = FUN_00406fc0(*(int *)((int)param_1 + *(int *)((int)param_1 + 0x348) * 0x1c + 0x30c));
  local_10 = FUN_00406fc0(*(int *)((int)param_1 + *(int *)((int)param_1 + 0x348) * 0x1c + 0x30c));
  iVar10 = iVar10 - iVar1;
  if (iVar10 < 1) {
    iVar10 = 1;
  }
  iVar6 = _rand();
  iVar7 = _rand();
  iVar3 = (((((iVar4 + iVar2 % iVar9) -
             iVar7 % *(int *)((int)param_1 + *(int *)((int)param_1 + 0x348) * 0x1c + 0x308)) -
            iVar6 % iVar10) - local_10) - iVar5) - iVar3;
  if ((*(int *)(*(int *)((int)param_1 + 0x2f0) + 0xc4) == 4) &&
     (0 < *(int *)(*(int *)((int)param_1 + 0x2f0) + 200))) {
    iVar9 = _rand();
    iVar3 = iVar3 + 1 + iVar9 % 3;
  }
  iVar9 = iVar1;
  if (0x28 < iVar1) {
    iVar9 = 0x28;
  }
  iVar10 = _rand();
  if ((iVar3 < 1) && (0x5b < iVar10 % 100 + iVar9 / 2)) {
    iVar3 = 1;
  }
  else if (iVar3 < 1) {
    return 0;
  }
  local_24 = (uint)(0x13 < *(int *)(*(int *)((int)param_1 + 0x2f0) + 0x60));
  iVar10 = FUN_00406fc0((local_8 - local_18) + 1);
  iVar10 = iVar10 + local_24 + local_18;
  if (0xf < iVar3) {
    iVar3 = FUN_00406fc0(iVar9 / 2);
    iVar10 = iVar10 + iVar3;
  }
  if (local_14 == 1) {
    iVar10 = iVar10 + 1;
  }
  if ((((*(short *)(*(int *)((int)param_1 + 0x2f0) + 0x6cc) == 2) && (iVar1 < 10)) && (2 < local_4))
     && (iVar3 = _rand(), 0x41 < iVar3 % 100)) {
    iVar10 = iVar10 + 1;
  }
  if ((*(int *)(*(int *)((int)param_1 + 0x2f0) + 0xc4) == 4) &&
     (0 < *(int *)(*(int *)((int)param_1 + 0x2f0) + 200))) {
    iVar3 = FUN_00406fc0(3);
    return iVar10 + 2 + iVar3;
  }
  return iVar10;
}


