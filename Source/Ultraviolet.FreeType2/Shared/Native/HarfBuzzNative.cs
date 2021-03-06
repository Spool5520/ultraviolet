﻿using System;
using System.Runtime.InteropServices;
using Ultraviolet.Core;
using Ultraviolet.Core.Native;

namespace Ultraviolet.FreeType2.Native
{
#pragma warning disable 1591
    public static class HarfBuzzNative
    {
#if ANDROID
        const String LIBRARY = "harfbuzz";
#elif IOS
        const String LIBRARY = "__Internal";
#else
        private static readonly NativeLibrary lib = new NativeLibrary(
            UltravioletPlatformInfo.CurrentPlatform == UltravioletPlatform.Windows ? "harfbuzz" : "libharfbuzz");
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr hb_buffer_create();
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr hb_buffer_createDelegate();
        private static readonly hb_buffer_createDelegate phb_buffer_create = lib.LoadFunction<hb_buffer_createDelegate>("hb_buffer_create");
        public static IntPtr hb_buffer_create() => phb_buffer_create();
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_destroy(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_destroyDelegate(IntPtr buffer);
        private static readonly hb_buffer_destroyDelegate phb_buffer_destroy = lib.LoadFunction<hb_buffer_destroyDelegate>("hb_buffer_destroy");
        public static void hb_buffer_destroy(IntPtr buffer) => phb_buffer_destroy(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_pre_allocate", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_pre_allocate(IntPtr buffer, UInt32 size);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_pre_allocateDelegate(IntPtr buffer, UInt32 size);
        private static readonly hb_buffer_pre_allocateDelegate phb_buffer_pre_allocate = lib.LoadFunction<hb_buffer_pre_allocateDelegate>("hb_buffer_pre_allocate");
        public static void hb_buffer_pre_allocate(IntPtr buffer, UInt32 size) => phb_buffer_pre_allocate(buffer, size);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_get_length", CallingConvention = CallingConvention.Cdecl)]
        public static extern UInt32 hb_buffer_get_length(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate UInt32 hb_buffer_get_lengthDelegate(IntPtr buffer);
        private static readonly hb_buffer_get_lengthDelegate phb_buffer_get_length = lib.LoadFunction<hb_buffer_get_lengthDelegate>("hb_buffer_get_length");
        public static UInt32 hb_buffer_get_length(IntPtr buffer) => phb_buffer_get_length(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_set_length", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern Boolean hb_buffer_set_length(IntPtr buffer, UInt32 size);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private delegate Boolean hb_buffer_set_lengthDelegate(IntPtr buffer, UInt32 size);
        private static readonly hb_buffer_set_lengthDelegate phb_buffer_set_length = lib.LoadFunction<hb_buffer_set_lengthDelegate>("hb_buffer_set_length");
        public static Boolean hb_buffer_set_length(IntPtr buffer, UInt32 size) => phb_buffer_set_length(buffer, size);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_reset", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_reset(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_resetDelegate(IntPtr buffer);
        private static readonly hb_buffer_resetDelegate phb_buffer_reset = lib.LoadFunction<hb_buffer_resetDelegate>("hb_buffer_reset");
        public static void hb_buffer_reset(IntPtr buffer) => phb_buffer_reset(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_clear_contents", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_clear_contents(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_clear_contentsDelegate(IntPtr buffer);
        private static readonly hb_buffer_clear_contentsDelegate phb_buffer_clear_contents = lib.LoadFunction<hb_buffer_clear_contentsDelegate>("hb_buffer_clear_contents");
        public static void hb_buffer_clear_contents(IntPtr buffer) => phb_buffer_clear_contents(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_add_utf16", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_add_utf16(IntPtr buffer, IntPtr text, Int32 textLength, UInt32 item_offset, Int32 item_length);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_add_utf16Delegate(IntPtr buffer, IntPtr text, Int32 textLength, UInt32 item_offset, Int32 item_length);
        private static readonly hb_buffer_add_utf16Delegate phb_buffer_add_utf16 = lib.LoadFunction<hb_buffer_add_utf16Delegate>("hb_buffer_add_utf16");
        public static void hb_buffer_add_utf16(IntPtr buffer, IntPtr text, Int32 textLength, UInt32 item_offset, Int32 item_length) => phb_buffer_add_utf16(buffer, text, textLength, item_offset, item_length);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_guess_segment_properties", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_guess_segment_properties(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_guess_segment_propertiesDelegate(IntPtr buffer);
        private static readonly hb_buffer_guess_segment_propertiesDelegate phb_buffer_guess_segment_properties = lib.LoadFunction<hb_buffer_guess_segment_propertiesDelegate>("hb_buffer_guess_segment_properties");
        public static void hb_buffer_guess_segment_properties(IntPtr buffer) => phb_buffer_guess_segment_properties(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_get_script", CallingConvention = CallingConvention.Cdecl)]
        public static extern hb_script_t hb_buffer_get_script(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate hb_script_t hb_buffer_get_scriptDelegate(IntPtr buffer);
        private static readonly hb_buffer_get_scriptDelegate phb_buffer_get_script = lib.LoadFunction<hb_buffer_get_scriptDelegate>("hb_buffer_get_script");
        public static hb_script_t hb_buffer_get_script(IntPtr buffer) => phb_buffer_get_script(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_set_script", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_set_script(IntPtr buffer, hb_script_t script);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_set_scriptDelegate(IntPtr buffer, hb_script_t script);
        private static readonly hb_buffer_set_scriptDelegate phb_buffer_set_script = lib.LoadFunction<hb_buffer_set_scriptDelegate>("hb_buffer_set_script");
        public static void hb_buffer_set_script(IntPtr buffer, hb_script_t script) => phb_buffer_set_script(buffer, script);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_get_language", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr hb_buffer_get_language(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr hb_buffer_get_languageDelegate(IntPtr buffer);
        private static readonly hb_buffer_get_languageDelegate phb_buffer_get_language = lib.LoadFunction<hb_buffer_get_languageDelegate>("hb_buffer_get_language");
        public static IntPtr hb_buffer_get_language(IntPtr buffer) => phb_buffer_get_language(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_set_language", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_set_language(IntPtr buffer, IntPtr language);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_set_languageDelegate(IntPtr buffer, IntPtr language);
        private static readonly hb_buffer_set_languageDelegate phb_buffer_set_language = lib.LoadFunction<hb_buffer_set_languageDelegate>("hb_buffer_set_language");
        public static void hb_buffer_set_language(IntPtr buffer, IntPtr language) => phb_buffer_set_language(buffer, language);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_get_direction", CallingConvention = CallingConvention.Cdecl)]
        public static extern hb_direction_t hb_buffer_get_direction(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate hb_direction_t hb_buffer_get_directionDelegate(IntPtr buffer);
        private static readonly hb_buffer_get_directionDelegate phb_buffer_get_direction = lib.LoadFunction<hb_buffer_get_directionDelegate>("hb_buffer_get_direction");
        public static hb_direction_t hb_buffer_get_direction(IntPtr buffer) => phb_buffer_get_direction(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_set_direction", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_buffer_set_direction(IntPtr buffer, hb_direction_t direction);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_buffer_set_directionDelegate(IntPtr buffer, hb_direction_t direction);
        private static readonly hb_buffer_set_directionDelegate phb_buffer_set_direction = lib.LoadFunction<hb_buffer_set_directionDelegate>("hb_buffer_set_direction");
        public static void hb_buffer_set_direction(IntPtr buffer, hb_direction_t direction) => phb_buffer_set_direction(buffer, direction);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_language_to_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr hb_language_to_string(IntPtr language);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr hb_language_to_stringDelegate(IntPtr language);
        private static readonly hb_language_to_stringDelegate phb_language_to_string = lib.LoadFunction<hb_language_to_stringDelegate>("hb_language_to_string");
        public static IntPtr hb_language_to_string(IntPtr language) => phb_language_to_string(language);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_language_from_string", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr hb_language_from_string(IntPtr str, Int32 len);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr hb_language_from_stringDelegate(IntPtr str, Int32 len);
        private static readonly hb_language_from_stringDelegate phb_language_from_string = lib.LoadFunction<hb_language_from_stringDelegate>("hb_language_from_string");
        public static IntPtr hb_language_from_string(IntPtr str, Int32 len) => phb_language_from_string(str, len);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_get_glyph_infos", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr hb_buffer_get_glyph_infos(IntPtr buffer, IntPtr length);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr hb_buffer_get_glyph_infosDelegate(IntPtr buffer, IntPtr length);
        private static readonly hb_buffer_get_glyph_infosDelegate phb_buffer_get_glyph_infos = lib.LoadFunction<hb_buffer_get_glyph_infosDelegate>("hb_buffer_get_glyph_infos");
        public static IntPtr hb_buffer_get_glyph_infos(IntPtr buffer, IntPtr length) => phb_buffer_get_glyph_infos(buffer, length);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_get_glyph_positions", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr hb_buffer_get_glyph_positions(IntPtr buffer, IntPtr length);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr hb_buffer_get_glyph_positionsDelegate(IntPtr buffer, IntPtr length);
        private static readonly hb_buffer_get_glyph_positionsDelegate phb_buffer_get_glyph_positions = lib.LoadFunction<hb_buffer_get_glyph_positionsDelegate>("hb_buffer_get_glyph_positions");
        public static IntPtr hb_buffer_get_glyph_positions(IntPtr buffer, IntPtr length) => phb_buffer_get_glyph_positions(buffer, length);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_buffer_get_content_type", CallingConvention = CallingConvention.Cdecl)]
        public static extern hb_buffer_content_type_t hb_buffer_get_content_type(IntPtr buffer);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate hb_buffer_content_type_t hb_buffer_get_content_typeDelegate(IntPtr buffer);
        private static readonly hb_buffer_get_content_typeDelegate phb_buffer_get_content_type = lib.LoadFunction<hb_buffer_get_content_typeDelegate>("hb_buffer_get_content_type");
        public static hb_buffer_content_type_t hb_buffer_get_content_type(IntPtr buffer) => phb_buffer_get_content_type(buffer);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_shape", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_shape(IntPtr font, IntPtr buffer, IntPtr features, UInt32 num_features);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_shapeDelegate(IntPtr font, IntPtr buffer, IntPtr features, UInt32 num_features);
        private static readonly hb_shapeDelegate phb_shape = lib.LoadFunction<hb_shapeDelegate>("hb_shape");
        public static void hb_shape(IntPtr font, IntPtr buffer, IntPtr features, UInt32 num_features) => phb_shape(font, buffer, features, num_features);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_ft_font_create", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr hb_ft_font_create(IntPtr ft_face, IntPtr destroy);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr hb_ft_font_createDelegate(IntPtr ft_face, IntPtr destroy);
        private static readonly hb_ft_font_createDelegate phb_ft_font_create = lib.LoadFunction<hb_ft_font_createDelegate>("hb_ft_font_create");
        public static IntPtr hb_ft_font_create(IntPtr ft_face, IntPtr destroy) => phb_ft_font_create(ft_face, destroy);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_font_destroy", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_font_destroy(IntPtr font);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_font_destroyDelegate(IntPtr font);
        private static readonly hb_font_destroyDelegate phb_font_destroy = lib.LoadFunction<hb_font_destroyDelegate>("hb_font_destroy");
        public static void hb_font_destroy(IntPtr font) => phb_font_destroy(font);
#endif

#if ANDROID || IOS
        [DllImport(LIBRARY, EntryPoint="hb_ft_font_set_load_flags", CallingConvention = CallingConvention.Cdecl)]
        public static extern void hb_ft_font_set_load_flags(IntPtr font, Int32 load_flags);
#else
        [MonoNativeFunctionWrapper]
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void hb_ft_font_set_load_flagsDelegate(IntPtr font, Int32 load_flags);
        private static readonly hb_ft_font_set_load_flagsDelegate phb_ft_font_set_load_flags = lib.LoadFunction<hb_ft_font_set_load_flagsDelegate>("hb_ft_font_set_load_flags");
        public static void hb_ft_font_set_load_flags(IntPtr font, Int32 load_flags) => phb_ft_font_set_load_flags(font, load_flags);
#endif
    }
#pragma warning restore 1591
}
