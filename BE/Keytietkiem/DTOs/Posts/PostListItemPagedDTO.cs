namespace Keytietkiem.DTOs.Post
{
    public class PostListItemPagedDTO
    {
        public List<PostListItemDTO> Data { get; set; }
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}