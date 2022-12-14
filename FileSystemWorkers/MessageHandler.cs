using System;

namespace FileSystemWork
{
    public class MessageHandler
    {
        private FilesSystemCreator _creator;

        public MessageHandler(FilesSystemCreator creator)
        {
            _creator = creator;
        }

        public void Handle(Message message)
        {
            switch (message.Type)
            {
                case MsgType.CreateDisk:
                    _creator.CreateDirectory(message.Id);
                    break;
                case MsgType.CreateDirectory:
                    _creator.CreateDirectory(message.Path);
                    break;
                case MsgType.DeleteDirectory:
                    _creator.RemoveDirectory(message.Path);
                    break;
                case MsgType.RenameDirectory:
                    _creator.RenameDirectory(message.Path, message.NewPath);
                    break;
                case MsgType.CreateFile:
                    _creator.CreateFile(message.Path, message.File);
                    break;
                case MsgType.DeleteFile:
                    _creator.RemoveFile(message.Path);
                    break;
                case MsgType.ChangeFile:
                    _creator.UpdateFile(message.Path, message.File);
                    break;
                case MsgType.RenameFile:
                    _creator.RenameFile(message.Path, message.NewPath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}