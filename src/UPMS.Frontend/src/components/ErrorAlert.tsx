import { CGIAlert } from 'eds-react-app';

export function ErrorAlert({ message, onClose }: { message?: string | null; onClose?: () => void }) {
  if (!message) {
    return null;
  }

  return (
    <CGIAlert
      type="error"
      title="Something went wrong"
      message={message}
      open={true}
      onClose={onClose ?? (() => undefined)}
      icon={true}
      autoClose={false}
    />
  );
}
