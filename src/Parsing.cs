namespace VkLib
{
    public static class Parser
    {
        public static long DomainStringToLong(string str)
        {
            if(str.Contains('|') && str.StartsWith('[') && str.EndsWith(']'))
            {
                int pos1 = str.IndexOf("id") + 2;
                int pos2 = str.IndexOf("|");
                int result;
                if(Int32.TryParse(str.Substring(pos1, pos2 - pos1), out result)) return result;
            }
            return 0;
        }
        public static string ToForwardString(long peer_id, List<long> conversation_message_ids, bool is_reply)
        {
            var sb = new StringBuilder();
            sb.Append("{")
                .Append("\"peer_id\":").Append(peer_id).Append(',')
                .Append("\"is_reply\":").Append(is_reply ? "true" : "false").Append(',')
                .Append("\"conversation_message_ids\":").Append('[');
            foreach(var id in conversation_message_ids)
                sb.Append(id).Append(',');

            // удаляем последнюю запятую
            sb.Remove(sb.Length - 1, 1);
            sb.Append("]}");
            return sb.ToString();
        }
    }
}