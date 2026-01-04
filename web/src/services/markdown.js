import axios from '../axiosConfig.js';

export const exportMarkdown = async (noteId) => {
  const response = await axios.post('/api/export_md', {
    id: noteId
  });
  return response.data;
};
