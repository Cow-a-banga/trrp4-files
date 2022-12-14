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
                case MsgType.Delete:
                    _creator.Delete(message.Path);
                    break;
                case MsgType.RenameDirectory:
                    _creator.RenameDirectory(message.Path, message.NewPath);
                    break;
                case MsgType.CreateFile:
                    _creator.CreateFile(message.Path, message.File);
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