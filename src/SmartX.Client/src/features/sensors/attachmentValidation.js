import {
  MAXIMUM_ATTACHMENT_SIZE_BYTES,
  getAttachmentCategory,
} from "../../api/attachmentOptions";

function getFileExtension(fileName) {
  const dotIndex = fileName.lastIndexOf(".");

  if (dotIndex < 0) {
    return "";
  }

  return fileName.slice(dotIndex).toLowerCase();
}

export function validateAttachment(file, categoryValue) {
  if (!(file instanceof File)) {
    return "Select a file to upload.";
  }

  if (file.size === 0) {
    return "The selected file is empty.";
  }

  if (file.size > MAXIMUM_ATTACHMENT_SIZE_BYTES) {
    return "The attachment cannot exceed 5 MB.";
  }

  if (file.name.length > 255) {
    return "The file name cannot exceed 255 characters.";
  }

  const category = getAttachmentCategory(categoryValue);

  if (!category) {
    return "Select a valid attachment category.";
  }

  const extension = getFileExtension(file.name);

  if (!category.allowedExtensions.includes(extension)) {
    return `${
      category.label
    } files must use: ${category.allowedExtensions.join(", ")}.`;
  }

  return null;
}
