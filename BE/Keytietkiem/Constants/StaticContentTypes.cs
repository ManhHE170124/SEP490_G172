/**
 * File: StaticContentTypes.cs
 * Author: HieuNDHE173169
 * Created: 31/12/2025
 * Version: 1.0.0
 * Purpose: Constants for static content PostTypes (Policy, UserGuide, AboutUs).
 *          These PostTypes have special validation rules: only 1 Post per type, cannot delete PostType.
 */
namespace Keytietkiem.Constants;

/// <summary>
/// Constants for static content PostTypes.
/// These are special PostTypes that should only have 1 Post each.
/// </summary>
public static class StaticContentTypes
{
    /// <summary>
    /// Policy PostType slug
    /// </summary>
    public const string POLICY = "policy";
    
    /// <summary>
    /// UserGuide PostType slug
    /// </summary>
    public const string USER_GUIDE = "user-guide";
    
    /// <summary>
    /// AboutUs PostType slug
    /// </summary>
    public const string ABOUT_US = "about-us";
    
    /// <summary>
    /// SpecificDocumentation PostType slug
    /// </summary>
    public const string SPECIFIC_DOCUMENTATION = "specific-documentation";
    
    /// <summary>
    /// All static content type slugs
    /// </summary>
    public static readonly string[] All = { POLICY, USER_GUIDE, ABOUT_US, SPECIFIC_DOCUMENTATION };
    
    /// <summary>
    /// Check if a slug is a static content type
    /// </summary>
    /// <param name="slug">The slug to check</param>
    /// <returns>True if the slug is a static content type</returns>
    public static bool IsStaticContent(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;
            
        return All.Contains(slug.ToLower());
    }
    
    /// <summary>
    /// Generate slug from PostTypeName
    /// </summary>
    /// <param name="postTypeName">The PostType name</param>
    /// <returns>The generated slug</returns>
    public static string GenerateSlugFromName(string postTypeName)
    {
        if (string.IsNullOrWhiteSpace(postTypeName))
            return string.Empty;
            
        // Convert to lowercase and replace spaces with hyphens
        return postTypeName.ToLower()
            .Replace(" ", "-")
            .Replace("_", "-");
    }
}

