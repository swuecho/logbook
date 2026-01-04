import axios from '../axiosConfig';

export const exportMarkdown = async (noteId: string) => {
  const response = await axios.post('/api/export_md', {
    id: noteId
  });
  return response.data;
};
