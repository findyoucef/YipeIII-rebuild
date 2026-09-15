// Add partial object layouts and correct high-confidence function signatures.
// @category Yipe

import ghidra.app.script.GhidraScript;
import ghidra.program.model.data.ArrayDataType;
import ghidra.program.model.data.BooleanDataType;
import ghidra.program.model.data.CategoryPath;
import ghidra.program.model.data.DataType;
import ghidra.program.model.data.DataTypeConflictHandler;
import ghidra.program.model.data.DataTypeManager;
import ghidra.program.model.data.DWordDataType;
import ghidra.program.model.data.IntegerDataType;
import ghidra.program.model.data.PointerDataType;
import ghidra.program.model.data.StructureDataType;
import ghidra.program.model.data.Undefined1DataType;
import ghidra.program.model.data.UnsignedShortDataType;
import ghidra.program.model.data.VoidDataType;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.SourceType;

public class ApplyYipeTypes extends GhidraScript {
    private DataTypeManager dataTypes;

    @Override
    public void run() throws Exception {
        dataTypes = currentProgram.getDataTypeManager();
        DataType player = createPlayerType();
        DataType itemNames = createItemNamesType();

        setThiscallSignature("00417e40", VoidDataType.dataType);

        setReturnType("00444a00", BooleanDataType.dataType);
        setReturnType("004042e0", IntegerDataType.dataType);
        setReturnType("00404a10", IntegerDataType.dataType);
        setReturnType("004583c8", IntegerDataType.dataType);

        requireFunction("00417e40").setComment(
            "Reads Yipe3Items.txt into the item-name subobject. Confirmed __thiscall: caller " +
            "0x0041a510 places this+0x198 in ECX. Skips comment/empty lines, limits names to " +
            "22 bytes, reads 30 armor and 38 weapon names, then uses resource fallback if incomplete.");
        requireFunction("004042e0").setComment(
            "Called for each living opponent. Returns integer damage to the player; callers use " +
            "the value arithmetically. The former pointer return was a decompiler inference error.");

        println("Created partial CPlayer and YipeItemNames types and corrected checked signatures.");
        println("Ghidra owns implicit this parameters; use CPlayer_partial and YipeItemNames " +
            "while reviewing the corresponding decompiler variables.");
    }

    private DataType createPlayerType() {
        StructureDataType player = new StructureDataType(
            new CategoryPath("/Yipe"), "CPlayer_partial", 0x6d0, dataTypes);
        put(player, 0x000, new PointerDataType(DWordDataType.dataType, 4, dataTypes),
            "vtable", "CPlayer virtual function table");
        put(player, 0x004, DWordDataType.dataType, "name_cstring", "MFC CString storage");
        put(player, 0x008, IntegerDataType.dataType, "dexterity", "Confirmed by item effects");
        put(player, 0x00c, IntegerDataType.dataType, "intelligence", "Confirmed by item effects");
        put(player, 0x010, IntegerDataType.dataType, "strength", "Confirmed by item effects");
        put(player, 0x014, IntegerDataType.dataType, "health", "Current hit points");
        put(player, 0x018, IntegerDataType.dataType, "maximum_health", "Maximum hit points");
        put(player, 0x01c, IntegerDataType.dataType, "unknown_001c", "Serialized 32-bit value");
        put(player, 0x020, IntegerDataType.dataType, "unknown_0020", "Serialized 32-bit value");
        put(player, 0x024, IntegerDataType.dataType, "spell_points", "Current spell points");
        put(player, 0x028, IntegerDataType.dataType, "maximum_spell_points", "Maximum spell points");
        put(player, 0x02c, IntegerDataType.dataType, "level", "Used by item and combat calculations");
        put(player, 0x030, IntegerDataType.dataType, "unknown_0030", "Serialized 32-bit value");
        put(player, 0x034, IntegerDataType.dataType, "unknown_0034", "Serialized 32-bit value");
        put(player, 0x038, new ArrayDataType(UnsignedShortDataType.dataType, 5, 2),
            "unknown_u16_0038", "Five serialized 16-bit values");
        put(player, 0x042, new ArrayDataType(UnsignedShortDataType.dataType, 4, 2),
            "unknown_u16_0042", "Four serialized 16-bit values");
        put(player, 0x04c, IntegerDataType.dataType, "unknown_004c", "Surface-potion comparison");
        put(player, 0x050, UnsignedShortDataType.dataType, "unknown_0050", "Serialized 16-bit value");
        put(player, 0x054, IntegerDataType.dataType, "unknown_0054", "Serialized 32-bit value");
        put(player, 0x058, IntegerDataType.dataType, "unknown_0058", "Stored after subtracting 203");
        put(player, 0x05c, IntegerDataType.dataType, "unknown_005c", "Serialized 32-bit value");
        put(player, 0x060, new ArrayDataType(Undefined1DataType.dataType, 13, 1),
            "unknown_flags_0060", "Includes flags at 0x62 and healing flag at 0x65");
        put(player, 0x070, IntegerDataType.dataType, "unknown_0070", "Serialized 32-bit value");
        put(player, 0x074, IntegerDataType.dataType, "unknown_0074", "Serialized 32-bit value");
        put(player, 0x078, IntegerDataType.dataType, "unknown_0078", "Serialized 32-bit value");
        put(player, 0x07c, new ArrayDataType(UnsignedShortDataType.dataType, 2, 2),
            "unknown_u16_007c", "Serialized 16-bit values");
        put(player, 0x080, new ArrayDataType(UnsignedShortDataType.dataType, 10, 2),
            "unknown_u16_0080", "Ten serialized 16-bit values");
        put(player, 0x094, new ArrayDataType(UnsignedShortDataType.dataType, 30, 2),
            "inventory_item_ids", "Thirty serialized item IDs");
        put(player, 0x0d0, new ArrayDataType(UnsignedShortDataType.dataType, 30, 2),
            "inventory_unknown_u16", "Paired serialized 16-bit values");
        put(player, 0x10c, new ArrayDataType(UnsignedShortDataType.dataType, 15, 2),
            "unknown_u16_010c", "Fifteen serialized 16-bit values");
        put(player, 0x12a, new ArrayDataType(Undefined1DataType.dataType, 30, 1),
            "inventory_flags", "Paired serialized boolean values");
        put(player, 0x148, new ArrayDataType(Undefined1DataType.dataType, 10, 1),
            "unknown_flags_0148", "Ten serialized boolean values");
        put(player, 0x152, new ArrayDataType(Undefined1DataType.dataType, 512, 1),
            "unknown_flags_0152", "First 512-element serialized flag array");
        put(player, 0x352, new ArrayDataType(Undefined1DataType.dataType, 512, 1),
            "unknown_flags_0352", "Second 512-element serialized flag array");
        put(player, 0x552, new ArrayDataType(Undefined1DataType.dataType, 50, 1),
            "unknown_flags_0552", "Fifty serialized boolean values");
        put(player, 0x584, new ArrayDataType(IntegerDataType.dataType, 50, 4),
            "unknown_i32_0584", "Fifty serialized 32-bit values");
        put(player, 0x64c, Undefined1DataType.dataType, "unknown_064c", "Serialized boolean");
        put(player, 0x64e, Undefined1DataType.dataType, "save_validation_passed", "Set after load validation");
        put(player, 0x64f, Undefined1DataType.dataType, "unknown_064f", "Initialized to true");
        put(player, 0x650, DWordDataType.dataType, "message_cstring", "MFC CString storage");
        put(player, 0x654, IntegerDataType.dataType, "stat_cap_mode", "Selects 30, 40, or 50 stat cap");
        put(player, 0x658, IntegerDataType.dataType, "unknown_0658", "Serialized 32-bit value");
        put(player, 0x65c, UnsignedShortDataType.dataType, "unknown_065c", "Serialized 16-bit value");
        put(player, 0x65e, new ArrayDataType(UnsignedShortDataType.dataType, 50, 2),
            "unknown_u16_065e", "Fifty serialized values; item/world state uses elements in this array");

        return dataTypes.addDataType(player, DataTypeConflictHandler.REPLACE_HANDLER);
    }

    private DataType createItemNamesType() {
        StructureDataType names = new StructureDataType(
            new CategoryPath("/Yipe"), "YipeItemNames", 0x114, dataTypes);
        put(names, 0x004, new ArrayDataType(DWordDataType.dataType, 30, 4),
            "armor_name_cstrings", "Thirty MFC CString handles");
        put(names, 0x07c, new ArrayDataType(DWordDataType.dataType, 38, 4),
            "weapon_name_cstrings", "Thirty-eight MFC CString handles");
        return dataTypes.addDataType(names, DataTypeConflictHandler.REPLACE_HANDLER);
    }

    private void put(StructureDataType structure, int offset, DataType type,
                     String name, String comment) {
        structure.replaceAtOffset(offset, type, type.getLength(), name, comment);
    }

    private void setThiscallSignature(String address, DataType returnType) throws Exception {
        Function function = requireFunction(address);
        function.setCallingConvention("__thiscall");
        function.setReturnType(returnType, SourceType.USER_DEFINED);
        function.replaceParameters(Function.FunctionUpdateType.DYNAMIC_STORAGE_ALL_PARAMS,
            true, SourceType.USER_DEFINED);
    }

    private void setReturnType(String address, DataType type) throws Exception {
        requireFunction(address).setReturnType(type, SourceType.USER_DEFINED);
    }

    private Function requireFunction(String address) {
        Function function = getFunctionAt(toAddr(address));
        if (function == null) {
            throw new IllegalStateException("Missing function " + address);
        }
        return function;
    }
}
