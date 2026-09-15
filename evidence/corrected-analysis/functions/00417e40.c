/* Address: 00417e40; Ghidra name: Yipe_LoadItemNames */

/* Reads Yipe3Items.txt into the item-name subobject. Confirmed __thiscall: caller 0x0041a510 places
   this+0x198 in ECX. Skips comment/empty lines, limits names to 22 bytes, reads 30 armor and 38
   weapon names, then uses resource fallback if incomplete. */

void __thiscall Yipe_LoadItemNames(void *this)

{
  bool bVar1;
  DWORD DVar2;
  int iVar3;
  int iVar4;
  int iVar5;
  int *piVar6;
  void *pvVar7;
  CHAR local_a18 [2500];
  int local_54 [4];
  undefined **local_44 [2];
  undefined4 local_3c;
  undefined4 local_38;
  undefined *local_34;
  int local_30;
  undefined4 local_2c;
  void *local_28;
  void *local_24;
  byte *local_20;
  byte *local_1c;
  char local_15;
  undefined1 *local_14;
  void *local_10;
  undefined1 *puStack_c;
  undefined4 local_8;
  
  local_8 = 0xffffffff;
  puStack_c = &LAB_00488ce0;
  local_10 = ExceptionList;
  local_14 = &stack0xfffff5dc;
  ExceptionList = &local_10;
  local_24 = this;
  FUN_0046df2c((undefined4 *)(local_a18 + 0x9c4));
  local_8 = 0;
  FUN_0046da88((int)local_44);
  local_34 = PTR_DAT_004a7f38;
  local_8._0_1_ = 2;
  local_44[0] = &PTR_FUN_00490320;
  local_3c = 0;
  local_38 = 0xffffffff;
  FUN_0046cf18(&local_34,(LPCSTR)0x0);
  local_8._0_1_ = 3;
  local_15 = '\0';
  bVar1 = false;
  FUN_0046cdfd(&local_20,&DAT_004abdf0);
  local_8._0_1_ = 4;
  FUN_0046db26(&local_2c);
  local_8 = CONCAT31(local_8._1_3_,6);
  DVar2 = FUN_0046e03e(local_a18 + 0x9c4,s_Yipe3Items_txt_004a6bec,0x20,(int)local_44);
  if (DVar2 == 0) {
    local_15 = '\x01';
  }
  else {
    iVar3 = FUN_0046e338((int *)(local_a18 + 0x9c4));
    if (iVar3 == 0x783) {
      bVar1 = true;
      local_15 = '\x01';
    }
    while (!bVar1) {
      DVar2 = FUN_0046e15b(local_a18 + 0x9c4,local_a18,0x9c4);
      FUN_0046d0f7(&local_20,local_a18);
      FUN_00465d07(&local_20,*(int *)(local_20 + -8) + -3,3);
      for (iVar3 = 0; iVar3 < 0x9c4; iVar3 = iVar3 + 1) {
        local_a18[iVar3] = ' ';
      }
      if (DVar2 < 0x9c4) {
        bVar1 = true;
      }
    }
    FUN_0046e259((int)(local_a18 + 0x9c4));
  }
  local_8 = 4;
  FUN_0046db64();
  iVar3 = FUN_00457e8a(local_20,&DAT_004abdf0);
  if ((iVar3 != 0) && (local_15 == '\0')) {
    bVar1 = false;
    local_1c = PTR_DAT_004a7f38;
    iVar3 = 0;
    local_28 = (void *)((int)local_24 + 4);
    local_8 = CONCAT31(local_8._1_3_,8);
    do {
      iVar4 = FUN_0046d1e2(&local_20,0xd);
      iVar4 = iVar4 + 1;
      if (iVar4 == 0) {
LAB_00417fcb:
        bVar1 = true;
        iVar4 = *(int *)(local_20 + -8);
      }
      else {
        iVar5 = FUN_00457e8a(local_20,&DAT_004abdf0);
        if (iVar5 == 0) goto LAB_00417fcb;
      }
      piVar6 = (int *)FUN_00465f77();
      local_8._0_1_ = 9;
      FUN_0046cec8(&local_1c,piVar6);
      local_8 = CONCAT31(local_8._1_3_,8);
      FUN_0046cd8f(&local_30);
      FUN_00466508((int *)&local_1c);
      FUN_004664bc((int *)&local_1c);
      FUN_00465d07(&local_20,0,iVar4);
      iVar4 = FUN_00466123(&local_1c,&DAT_004a6be8);
      if (iVar4 == -1) {
        iVar4 = FUN_00457e8a(local_1c,&DAT_004abdf0);
        if (iVar4 != 0) {
          if (0x16 < *(int *)(local_1c + -8)) {
            FUN_00465d07(&local_1c,0x16,*(int *)(local_1c + -8) + -0x16);
          }
          if (iVar3 < 0x1e) {
            FUN_0046cec8(local_28,(int *)&local_1c);
          }
          else if (iVar3 < 0x44) {
            FUN_0046cec8(local_28,(int *)&local_1c);
          }
          else {
            bVar1 = true;
          }
          iVar3 = iVar3 + 1;
          local_28 = (void *)((int)local_28 + 4);
        }
      }
    } while (!bVar1);
    if (iVar3 < 0x44) {
      local_15 = '\x01';
    }
    local_8 = CONCAT31(local_8._1_3_,4);
    FUN_0046cd8f((int *)&local_1c);
    if (local_15 == '\0') goto LAB_00418134;
  }
  local_1c = PTR_DAT_004a7f38;
  local_8 = CONCAT31(local_8._1_3_,10);
  iVar3 = 0;
  pvVar7 = local_24;
  do {
    pvVar7 = (void *)((int)pvVar7 + 4);
    FUN_0046d2f6(iVar3 + 0x15f);
    FUN_0046cec8(pvVar7,(int *)&local_1c);
    iVar3 = iVar3 + 1;
  } while (iVar3 < 0x1e);
  iVar3 = 0;
  pvVar7 = (void *)((int)local_24 + 0x7c);
  do {
    FUN_0046d2f6(iVar3 + 0x12d);
    FUN_0046cec8(pvVar7,(int *)&local_1c);
    iVar3 = iVar3 + 1;
    pvVar7 = (void *)((int)pvVar7 + 4);
  } while (iVar3 < 0x26);
  local_8 = CONCAT31(local_8._1_3_,4);
  FUN_0046cd8f((int *)&local_1c);
LAB_00418134:
  local_8._0_1_ = 3;
  FUN_0046cd8f((int *)&local_20);
  local_44[0] = &PTR_FUN_00490320;
  local_8 = CONCAT31(local_8._1_3_,0xb);
  FUN_0046cd8f((int *)&local_34);
  local_8 = 0xffffffff;
  FUN_0046df81();
  ExceptionList = local_10;
  return;
}


