import { Button, Group, Modal, Stack, TextInput, Textarea } from '@mantine/core';
import { useForm } from '@mantine/form';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '@/api/client';
import { BranchSelect } from '@/components';
import { useActiveBranch } from '@/lib/branches';
import { notifyError, notifySuccess } from '@/lib/notify';
import { requiredError } from '@/lib/validation';
import type { DocumentKind } from './documents';
import { detailKey, listKey } from './useDocument';

interface CreateDocumentModalProps {
  kind: DocumentKind;
  opened: boolean;
  onClose: () => void;
}

export function CreateDocumentModal({ kind, opened, onClose }: CreateDocumentModalProps) {
  const [activeBranch] = useActiveBranch();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const form = useForm({
    validateInputOnBlur: true,
    initialValues: { branchId: activeBranch, supplierName: '', notes: '' },
    validate: {
      branchId: (v) => (v ? null : 'Selecciona una sucursal'),
      supplierName: kind.hasSupplier ? requiredError('El proveedor') : undefined,
    },
  });

  useEffect(() => {
    if (opened) form.setValues({ branchId: activeBranch });
    // Reset the default branch each time the modal opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opened]);

  const create = useMutation({
    mutationFn: (values: typeof form.values) =>
      kind.api.create({
        branchId: values.branchId,
        supplierName: kind.hasSupplier ? values.supplierName.trim() : undefined,
        notes: values.notes.trim() || null,
      }),
    onSuccess: (doc) => {
      queryClient.setQueryData(detailKey(kind, doc.id), doc);
      void queryClient.invalidateQueries({ queryKey: listKey(kind) });
      notifySuccess(`Borrador de ${kind.singular} creado`);
      form.reset();
      onClose();
      navigate(`${kind.path}/${doc.id}`);
    },
    onError: (error) => {
      if (error instanceof ApiError && Object.keys(error.formErrors()).length) form.setErrors(error.formErrors());
      else notifyError(error, `No se pudo crear la ${kind.singular}`);
    },
  });

  return (
    <Modal opened={opened} onClose={onClose} title={`Nueva ${kind.singular}`} closeOnClickOutside={!create.isPending}>
      <form onSubmit={form.onSubmit((values) => create.mutate(values))} noValidate>
        <Stack gap="sm">
          <BranchSelect
            label="Sucursal"
            description={kind.hasSupplier ? 'Donde ingresará la mercancía' : 'De donde saldrá el stock'}
            withAsterisk
            allowDeselect={false}
            {...form.getInputProps('branchId')}
          />
          {kind.hasSupplier && (
            <TextInput
              label="Proveedor"
              placeholder="Nombre del proveedor"
              withAsterisk
              maxLength={200}
              {...form.getInputProps('supplierName')}
            />
          )}
          <Textarea
            label="Notas"
            description="Opcional"
            autosize
            minRows={2}
            maxRows={5}
            maxLength={1000}
            {...form.getInputProps('notes')}
          />
          <Group justify="flex-end" mt="xs">
            <Button variant="default" onClick={onClose} disabled={create.isPending}>Volver</Button>
            <Button type="submit" loading={create.isPending}>{`Crear ${kind.singular}`}</Button>
          </Group>
        </Stack>
      </form>
    </Modal>
  );
}
