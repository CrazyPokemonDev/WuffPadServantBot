using Newtonsoft.Json;
using System.Collections.Generic;

namespace WuffPadServantBot
{
    [JsonObject]
    public class TgWWResult
    {
        [JsonProperty(PropertyName = "success")]
        public bool Success { get; set; }

        [JsonProperty(PropertyName = "annotations")]
        public List<TgWWAnnotation> Annotations { get; set; }
    }

    public class TgWWAnnotation
    {
        [JsonProperty(PropertyName = "file")]
        public TgWWFile File { get; set; }

        [JsonProperty(PropertyName = "errors")]
        public List<List<object>> Errors { get; set; }

        [JsonProperty(PropertyName = "messages")]
        public List<List<object>> Messages { get; set; }
    }

    public enum TgWWFile
    {
        NoParticularFile = 0,
        ModelFile = 1,
        BaseFile = 2,
        TargetFile = 3
    }

    public enum TgWWMessageCode : long // This needs to inherit long
    {
        /// <summary>
        /// A string with key `details[0]` is missing.
        /// </summary>
        MissingString = 0,

        /// <summary>
        /// A string with unknown key, `details[0]`, is not present in the model langfile.
        /// </summary>
        UnknownString = 1,

        /// <summary>
        /// A string with key `details[0]` lacks placeholder `details[1]`.
        /// </summary>
        MissingPlaceholder = 2,

        /// <summary>
        /// A string with key `details[0]` has an extra placeholder, `details[1]`.
        /// </summary>
        ExtraPlaceholder = 3,

        /// <summary>
        /// Successfully added string with key `details[0]`.
        /// </summary>
        StringAdded = 4,

        /// <summary>
        /// Model langfile is not found, thus only partial validation is being performed.
        /// </summary>
        PartialValidation = 10,

        /// <summary>
        /// Model langfile does not have `isDefault=\"true\"` attribute in its language tag.
        /// </summary>
        ModelNotDefault = 11,

        /// <summary>
        /// The langfile is closed. `details[0]` is its owner.
        /// </summary>
        LangFileClosed = 12,

        /// <summary>
        /// A required attribute of the language tag, `details[0]`, is empty.
        /// </summary>
        LanguageTagFieldEmpty = 13,

        /// <summary>
        /// There are multiple strings with the same key, `details[0]`.
        /// </summary>
        DuplicatedString = 14,

        /// <summary>
        /// A string with key `details[0]` has an empty value.
        /// </summary>
        ValueEmpty = 15,

        /// <summary>
        /// A string with key `details[0]` has no values at all.
        /// </summary>
        ValuesMissing = 16,

        /// <summary>
        /// The langfile's name, which is `details[0]`, is the same as the name of its base or model.
        /// </summary>
        LangFileNameDuplication = 17,

        /// <summary>
        /// The langfile's base+variant pair, which is `details[0]` and `details[1]`, is the same as base+variant pair of its base or model.
        /// </summary>
        LangFileBaseVariantDuplication = 18,

        /// <summary>
        /// Placeholders are inconsistent across multiple values of a string with key `details[0]`.
        /// </summary>
        InconsistentPlaceholders = 19,

        /// <summary>
        /// A string with key `details[0]` has attribute `details[1]` set to `true`, however, in model langfile, it is set to `false`.
        /// </summary>
        AttributeWronglyTrue = 20
    }
}
